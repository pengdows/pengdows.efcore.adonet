using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet;

public static class AdoNetBulkExtensions
{
    /// <summary>
    ///     Inserts <paramref name="entities" /> without tracking them, through the dialect's <see cref="IAdoNetBulkLoader" />.
    ///     Keys the database generates are not read back, except those drawn from a sequence before the load
    ///     (<see cref="SequenceKeyRetrieval" />), which are set on the entities that have none: one round trip per key. If the
    ///     load fails they are reset. Database defaults don't apply: every mapped column is written. Joins the current
    ///     transaction, or loads all-or-nothing in one of its own.
    /// </summary>
    /// <returns>The number of rows loaded.</returns>
    public static long BulkInsert<TEntity>(this DbContext context, IEnumerable<TEntity> entities)
        where TEntity : class
    {
        var plan = Plan<TEntity>(context);
        var list = entities.ToList();
        var database = context.Database;

        database.OpenConnection();
        try
        {
            using var own = database.CurrentTransaction == null ? database.BeginTransaction() : null;
            var transaction = database.CurrentTransaction!.GetDbTransaction();
            var connection = database.GetDbConnection();
            var drawn = new List<TEntity>();
            try
            {
                if (plan.SequenceKey is { } sequenceKey)
                {
                    foreach (var entity in list.Where(sequenceKey.IsUnset))
                    {
                        sequenceKey.Set(entity, NextValue(connection, transaction, sequenceKey));
                        drawn.Add(entity);
                    }
                }

                context.GetService<IAdoNetBulkLoader>().Load(plan.Load(context, list, connection, transaction));
                own?.Commit();
            }
            catch
            {
                plan.SequenceKey?.Unset(drawn);
                throw;
            }
        }
        finally
        {
            database.CloseConnection();
        }

        return list.Count;
    }

    /// <inheritdoc cref="BulkInsert{TEntity}" />
    public static async Task<long> BulkInsertAsync<TEntity>(
        this DbContext context,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var plan = Plan<TEntity>(context);
        var list = entities.ToList();
        var database = context.Database;

        await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var own = database.CurrentTransaction == null
                ? await database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
                : null;
            var transaction = database.CurrentTransaction!.GetDbTransaction();
            var connection = database.GetDbConnection();
            var drawn = new List<TEntity>();
            try
            {
                if (plan.SequenceKey is { } sequenceKey)
                {
                    foreach (var entity in list.Where(sequenceKey.IsUnset))
                    {
                        sequenceKey.Set(entity, await NextValueAsync(connection, transaction, sequenceKey, cancellationToken).ConfigureAwait(false));
                        drawn.Add(entity);
                    }
                }

                await context.GetService<IAdoNetBulkLoader>()
                    .LoadAsync(plan.Load(context, list, connection, transaction), cancellationToken)
                    .ConfigureAwait(false);

                if (own != null)
                {
                    await own.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                plan.SequenceKey?.Unset(drawn);
                throw;
            }
        }
        finally
        {
            await database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return list.Count;
    }

    private static object? NextValue(DbConnection connection, DbTransaction transaction, SequenceKey key)
    {
        using var command = key.Command(connection, transaction);
        return command.ExecuteScalar();
    }

    private static async Task<object?> NextValueAsync(
        DbConnection connection,
        DbTransaction transaction,
        SequenceKey key,
        CancellationToken cancellationToken)
    {
        await using var command = key.Command(connection, transaction);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static BulkPlan Plan<TEntity>(DbContext context)
    {
        var dialect = context.GetService<IDbContextOptions>().FindExtension<Infrastructure.AdoNetOptionsExtension>()?.Dialect
            ?? throw new InvalidOperationException("BulkInsert needs a context configured with UseAdoNet.");
        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"'{typeof(TEntity).Name}' is not an entity type in this context's model.");

        if (entityType.GetComplexProperties().Any()
            || entityType.GetNavigations().Any(n => n.TargetEntityType.IsOwned())
            || entityType.GetTableName() is null)
        {
            throw new NotSupportedException($"BulkInsert supports entity types mapped to one table, without owned or complex types; '{entityType.DisplayName()}' isn't.");
        }

        // The design-time model keeps the annotations the runtime model drops.
        var designEntityType = context.GetService<IDesignTimeModel>().Model.FindEntityType(entityType.Name)!;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        var helper = context.GetService<ISqlGenerationHelper>();
        var typeMappingSource = context.GetService<IRelationalTypeMappingSource>();
        var columns = new List<(IProperty Property, BulkColumn Column)>();
        SequenceKey? sequenceKey = null;

        foreach (var property in entityType.GetProperties())
        {
            if (property.GetComputedColumnSql(table) != null)
            {
                continue;
            }

            if (property.IsShadowProperty() && property != entityType.FindDiscriminatorProperty())
            {
                throw new NotSupportedException(
                    $"BulkInsert reads values from the entities, but '{entityType.DisplayName()}.{property.Name}' is a shadow property "
                    + "with no value on them. Map it to a CLR property.");
            }

            if (designEntityType.FindProperty(property.Name)!.FindAnnotation(AdoNetAnnotationNames.Identity)?.Value is true)
            {
                if (dialect.Sql.Keys is not SequenceKeyRetrieval keys)
                {
                    continue;
                }

                var sequence = helper.DelimitIdentifier(keys.GetSequenceName(table.Name, property.GetColumnName(table)!), table.Schema);
                sequenceKey = new SequenceKey(property, keys.GetNextValueSql(sequence));
            }

            columns.Add((property, new BulkColumn(property.GetColumnName(table)!, ProviderTypeMapping(property, typeMappingSource))));
        }

        return new BulkPlan(entityType, table, columns, sequenceKey);
    }

    // Rows hold provider values, so they bind through the mapping of the provider type: binding them through the property's
    // own mapping would run its converter a second time.
    private static RelationalTypeMapping ProviderTypeMapping(IProperty property, IRelationalTypeMappingSource typeMappingSource)
    {
        var mapping = property.GetRelationalTypeMapping();
        if (mapping.Converter is not { } converter)
        {
            return mapping;
        }

        return typeMappingSource.FindMapping(converter.ProviderClrType, mapping.StoreType)
            ?? throw new NotSupportedException(
                $"BulkInsert found no mapping for '{converter.ProviderClrType.Name}' values in a '{mapping.StoreType}' column, "
                + $"which '{property.DeclaringType.DisplayName()}.{property.Name}' converts to.");
    }

    private sealed record BulkPlan(
        IEntityType EntityType,
        StoreObjectIdentifier Table,
        List<(IProperty Property, BulkColumn Column)> Columns,
        SequenceKey? SequenceKey)
    {
        public BulkLoad Load<TEntity>(DbContext context, List<TEntity> entities, DbConnection connection, DbTransaction transaction)
            where TEntity : class
            => new()
            {
                Context = context,
                Table = Table.Name,
                Schema = Table.Schema,
                Columns = Columns.Select(c => c.Column).ToList(),
                Rows = entities.Select(Row).ToList(),
                Connection = connection,
                Transaction = transaction
            };

        private object?[] Row(object entity)
        {
            var row = new object?[Columns.Count];
            for (var i = 0; i < Columns.Count; i++)
            {
                var property = Columns[i].Property;
                var value = property.IsShadowProperty()
                    ? EntityType.GetDiscriminatorValue()
                    : property.GetGetter().GetClrValueUsingContainingEntity(entity);
                var converter = property.GetRelationalTypeMapping().Converter;
                row[i] = (value != null) && (converter != null) ? converter.ConvertToProvider(value) : value;
            }

            return row;
        }
    }

    private sealed record SequenceKey(IProperty Property, string NextValueSql)
    {
        public DbCommand Command(DbConnection connection, DbTransaction transaction)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = NextValueSql;
            return command;
        }

        /// <summary>Whether the entity still has the key's sentinel, so the database is to supply it; a key set by hand is kept.</summary>
        public bool IsUnset(object entity)
            => Property.GetGetter().HasSentinelValueUsingContainingEntity(entity);

        /// <summary>Puts the sentinel back on entities whose drawn keys were never saved.</summary>
        public void Unset(IEnumerable<object> entities)
        {
            foreach (var entity in entities)
            {
                Write(entity, Property.Sentinel);
            }
        }

        public void Set(object entity, object? value)
            => Write(entity, Convert.ChangeType(value, Nullable.GetUnderlyingType(Property.ClrType) ?? Property.ClrType, System.Globalization.CultureInfo.InvariantCulture));

        private void Write(object entity, object? key)
        {
            if (Property.PropertyInfo is { CanWrite: true } propertyInfo)
            {
                propertyInfo.SetValue(entity, key);
            }
            else
            {
                Property.FieldInfo?.SetValue(entity, key);
            }
        }
    }
}

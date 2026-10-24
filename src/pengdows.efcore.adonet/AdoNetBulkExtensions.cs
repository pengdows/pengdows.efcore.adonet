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
    ///     (<see cref="SequenceKeyRetrieval" />), which are set on the entities. Database defaults don't apply: every mapped
    ///     column is written. Joins the current transaction, or loads all-or-nothing in one of its own.
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
            if (plan.SequenceKey is { } sequenceKey)
            {
                foreach (var entity in list)
                {
                    sequenceKey.Set(entity, NextValue(connection, transaction, sequenceKey));
                }
            }

            context.GetService<IAdoNetBulkLoader>().Load(plan.Load(context, list, connection, transaction));
            own?.Commit();
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
            if (plan.SequenceKey is { } sequenceKey)
            {
                foreach (var entity in list)
                {
                    sequenceKey.Set(entity, await NextValueAsync(connection, transaction, sequenceKey, cancellationToken).ConfigureAwait(false));
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
        var columns = new List<(IProperty Property, BulkColumn Column)>();
        SequenceKey? sequenceKey = null;

        foreach (var property in entityType.GetProperties())
        {
            if (property.GetComputedColumnSql(table) != null)
            {
                continue;
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

            columns.Add((property, new BulkColumn(property.GetColumnName(table)!, property.GetRelationalTypeMapping())));
        }

        return new BulkPlan(entityType, table, columns, sequenceKey);
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
                var (property, column) = Columns[i];
                var value = property.IsShadowProperty()
                    ? property == EntityType.FindDiscriminatorProperty() ? EntityType.GetDiscriminatorValue() : null
                    : property.GetGetter().GetClrValueUsingContainingEntity(entity);
                row[i] = value != null && column.TypeMapping.Converter is { } converter ? converter.ConvertToProvider(value) : value;
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

        public void Set(object entity, object? value)
        {
            var key = Convert.ChangeType(value, Nullable.GetUnderlyingType(Property.ClrType) ?? Property.ClrType, System.Globalization.CultureInfo.InvariantCulture);
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

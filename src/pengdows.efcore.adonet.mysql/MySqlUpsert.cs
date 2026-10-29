using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL's <c>INSERT ... ON DUPLICATE KEY UPDATE</c>, for an upsert EF has no translation for. Values come from
///     <paramref name="entity" />'s current property values, not the change tracker: this never tracks the entity, a
///     generated key or other server default is not read back, and the statement runs on its own, outside whatever
///     batching <c>SaveChanges</c> would give it. <c>ON DUPLICATE KEY UPDATE col = VALUES(col)</c> is used rather than
///     MySQL 8.0.19's row-alias form (<c>... AS new ON DUPLICATE KEY UPDATE col = new.col</c>) so the same statement
///     runs on MariaDB and older MySQL too, which have no row alias. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/1217f926be3771c016eb26b8192119569902d215/src/EFCore.MySql/Extensions/MySqlUpsertExtensions.cs
/// </summary>
public static class MySqlUpsertExtensions
{
    /// <summary>
    ///     Inserts <paramref name="entity" />, or updates <paramref name="updateProperties" /> (every mapped property but
    ///     <paramref name="keyProperties" />, by default) where a unique index or key it maps to conflicts.
    ///     <paramref name="keyProperties" /> names what the update excludes; MySQL finds the conflicting row itself, from
    ///     whatever unique index the insert violates, not from these names.
    /// </summary>
    /// <returns>
    ///     The row count MySQL reports: 1 for an insert, 2 for an update that changed a value. An update that leaves every
    ///     value as it was reports 0, unless the connection's <c>Use Affected Rows</c> option is <c>false</c> (this
    ///     provider's default, <see cref="MySqlDialect.ConnectionOptions" />), which makes it report 1 there too.
    /// </returns>
    public static Task<int> UpsertAsync<TEntity>(
        this DbContext context,
        TEntity entity,
        string[] keyProperties,
        string[]? updateProperties = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(keyProperties);

        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity)} is not an entity type of this context's model.");
        var table = entityType.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntity)} maps to no table.");
        var helper = context.GetService<ISqlGenerationHelper>();

        var properties = entityType.GetProperties().Where(p => !p.IsShadowProperty()).ToList();
        var keys = new HashSet<string>(keyProperties);
        var updated = updateProperties?.Select(name => properties.Single(p => p.Name == name))
            ?? properties.Where(p => !keys.Contains(p.Name));

        var columns = properties.Select(p => helper.DelimitIdentifier(ColumnName(p))).ToArray();
        var values = properties.Select(p => ProviderValue(p, entity)).ToArray();
        var placeholders = Enumerable.Range(0, properties.Count).Select(i => $"{{{i}}}");
        var updateClauses = updated.Select(p =>
        {
            var column = helper.DelimitIdentifier(ColumnName(p));
            return $"{column} = VALUES({column})";
        }).ToList();

        if (updateClauses.Count == 0)
        {
            throw new InvalidOperationException(
                $"Every property of {typeof(TEntity)} is a key property: nothing is left to update on conflict.");
        }

        var sql = $"INSERT INTO {helper.DelimitIdentifier(table)} ({string.Join(", ", columns)}) "
            + $"VALUES ({string.Join(", ", placeholders)}) ON DUPLICATE KEY UPDATE {string.Join(", ", updateClauses)}";

        return context.Database.ExecuteSqlRawAsync(sql, values, cancellationToken);
    }

    private static string ColumnName(IProperty property)
        => property.GetColumnName() ?? property.Name;

    private static object ProviderValue(IProperty property, object entity)
    {
        var value = property.PropertyInfo?.GetValue(entity) ?? property.FieldInfo?.GetValue(entity);
        if (value == null)
        {
            return DBNull.Value;
        }

        var converter = property.GetValueConverter() ?? property.FindTypeMapping()?.Converter;
        return converter?.ConvertToProvider(value) ?? value;
    }
}

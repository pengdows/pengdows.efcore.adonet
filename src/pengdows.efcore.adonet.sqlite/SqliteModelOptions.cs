using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace pengdows.efcore.adonet.sqlite;

/// <summary>
///     SQLite's model options, as Microsoft's SQLite provider has them: a table that can't take RETURNING (one with triggers,
///     or a virtual table) reads its keys with <c>last_insert_rowid()</c> and checks its rows with <c>changes()</c>; a column
///     made the table's <c>AUTOINCREMENT</c> key. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Extensions/SqliteTableBuilderExtensions.cs
/// </summary>
public static class SqliteModelOptionsExtensions
{
    /// <summary>Whether the table's inserts, updates and deletes use RETURNING; false for a table with triggers or a virtual table.</summary>
    public static TableBuilder UseSqlReturningClause(this TableBuilder tableBuilder, bool useSqlReturningClause = true)
    {
        tableBuilder.Metadata.SetAnnotation(UseSqlReturningClauseAnnotation, useSqlReturningClause);
        return tableBuilder;
    }

    /// <inheritdoc cref="UseSqlReturningClause(TableBuilder, bool)" />
    public static TableBuilder<TEntity> UseSqlReturningClause<TEntity>(this TableBuilder<TEntity> tableBuilder, bool useSqlReturningClause = true)
        where TEntity : class
        => (TableBuilder<TEntity>)UseSqlReturningClause((TableBuilder)tableBuilder, useSqlReturningClause);

    /// <inheritdoc cref="UseSqlReturningClause(TableBuilder, bool)" />
    public static OwnedNavigationTableBuilder UseSqlReturningClause(this OwnedNavigationTableBuilder tableBuilder, bool useSqlReturningClause = true)
    {
        tableBuilder.Metadata.SetAnnotation(UseSqlReturningClauseAnnotation, useSqlReturningClause);
        return tableBuilder;
    }

    /// <inheritdoc cref="UseSqlReturningClause(TableBuilder, bool)" />
    public static OwnedNavigationTableBuilder<TOwnerEntity, TDependentEntity> UseSqlReturningClause<TOwnerEntity, TDependentEntity>(
        this OwnedNavigationTableBuilder<TOwnerEntity, TDependentEntity> tableBuilder,
        bool useSqlReturningClause = true)
        where TOwnerEntity : class
        where TDependentEntity : class
        => (OwnedNavigationTableBuilder<TOwnerEntity, TDependentEntity>)UseSqlReturningClause(
            (OwnedNavigationTableBuilder)tableBuilder, useSqlReturningClause);

    /// <summary>Whether one table of an entity type split across tables uses RETURNING; false for a table with triggers.</summary>
    public static SplitTableBuilder UseSqlReturningClause(this SplitTableBuilder tableBuilder, bool useSqlReturningClause = true)
    {
        tableBuilder.MappingFragment.SetAnnotation(UseSqlReturningClauseAnnotation, useSqlReturningClause);
        return tableBuilder;
    }

    /// <inheritdoc cref="UseSqlReturningClause(SplitTableBuilder, bool)" />
    public static SplitTableBuilder<TEntity> UseSqlReturningClause<TEntity>(this SplitTableBuilder<TEntity> tableBuilder, bool useSqlReturningClause = true)
        where TEntity : class
        => (SplitTableBuilder<TEntity>)UseSqlReturningClause((SplitTableBuilder)tableBuilder, useSqlReturningClause);

    /// <summary>Whether the entity type's table uses RETURNING (true unless <see cref="UseSqlReturningClause(TableBuilder, bool)" /> said not).</summary>
    public static bool IsSqlReturningClauseUsed(this IReadOnlyEntityType entityType)
        => ReturningClauseSetting(entityType, UseSqlReturningClauseAnnotation)
            ?? ReturningClauseSetting(entityType, AdoNetAnnotationNames.UseReturningClause)
            ?? true;

    // The root type's setting, else the type's own; a type's annotation under this provider's earlier name still counts.
    private static bool? ReturningClauseSetting(IReadOnlyEntityType entityType, string annotation)
        => entityType.GetRootType().FindAnnotation(annotation)?.Value as bool?
            ?? entityType.FindAnnotation(annotation)?.Value as bool?;

    /// <summary>
    ///     Makes the column the table's <c>INTEGER PRIMARY KEY AUTOINCREMENT</c>, never reusing a deleted row's key: the core's
    ///     <see cref="AdoNetPropertyBuilderExtensions.UseIdentityColumn(PropertyBuilder)" /> by Microsoft's name. A single-column
    ///     integer key is one by convention.
    /// </summary>
    public static PropertyBuilder UseAutoincrement(this PropertyBuilder propertyBuilder)
        => propertyBuilder.UseIdentityColumn();

    /// <inheritdoc cref="UseAutoincrement(PropertyBuilder)" />
    public static PropertyBuilder<TProperty> UseAutoincrement<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => propertyBuilder.UseIdentityColumn();

    internal const string UseSqlReturningClauseAnnotation = "Sqlite:UseSqlReturningClause";

    internal const string SridAnnotation = "Sqlite:Srid";

    // The name this provider wrote before it adopted Microsoft's: still read, so existing models keep their SRID.
    internal const string LegacySridAnnotation = "AdoNet:Sqlite:Srid";

    /// <summary>
    ///     The spatial reference system of a SpatiaLite geometry column, which AddGeometryColumn declares it with and its values
    ///     must have. Behaviour reference (not copied):
    ///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Extensions/SqlitePropertyBuilderExtensions.cs
    /// </summary>
    public static PropertyBuilder HasSrid(this PropertyBuilder propertyBuilder, int srid)
        => propertyBuilder.HasAnnotation(SridAnnotation, srid);

    /// <inheritdoc cref="HasSrid(PropertyBuilder, int)" />
    public static PropertyBuilder<TProperty> HasSrid<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, int srid)
        => propertyBuilder.HasAnnotation(SridAnnotation, srid);

    /// <summary>The column's SRID from <see cref="HasSrid(PropertyBuilder, int)" />, or null.</summary>
    public static int? GetSrid(this IReadOnlyProperty property)
        => (property.FindAnnotation(SridAnnotation) ?? property.FindAnnotation(LegacySridAnnotation))?.Value as int?;

    /// <summary>Whether the context's database is SQLite: its dialect is <see cref="SqliteDialect" /> or a variant of it.</summary>
    public static bool IsSqlite(this DatabaseFacade database)
        => Microsoft.EntityFrameworkCore.SqliteDatabaseFacadeExtensions.IsSqlite(database);
}

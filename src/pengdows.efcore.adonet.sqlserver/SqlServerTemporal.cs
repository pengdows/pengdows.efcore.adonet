using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     Makes a table a system-versioned temporal table, as Microsoft's provider does: SQL Server keeps every row's earlier
///     versions in a history table, stamped by two hidden period columns. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerTableBuilderExtensions.cs
/// </summary>
public static class SqlServerTableBuilderExtensions
{
    /// <summary>Makes the table temporal (or not); the returned builder names its period columns and history table.</summary>
    public static TemporalTableBuilder IsTemporal(this TableBuilder tableBuilder, bool temporal = true)
    {
        tableBuilder.Metadata.SetAnnotation(SqlServerAnnotationNames.IsTemporal, temporal);
        return new TemporalTableBuilder(tableBuilder.Metadata);
    }

    /// <summary>Makes the table temporal; <paramref name="buildAction" /> names its period columns and history table.</summary>
    public static TableBuilder IsTemporal(this TableBuilder tableBuilder, Action<TemporalTableBuilder> buildAction)
    {
        buildAction(tableBuilder.IsTemporal());
        return tableBuilder;
    }

    /// <summary>Makes the table temporal (or not); the returned builder names its period columns and history table.</summary>
    public static TemporalTableBuilder<TEntity> IsTemporal<TEntity>(this TableBuilder<TEntity> tableBuilder, bool temporal = true)
        where TEntity : class
    {
        tableBuilder.Metadata.SetAnnotation(SqlServerAnnotationNames.IsTemporal, temporal);
        return new TemporalTableBuilder<TEntity>(tableBuilder.Metadata);
    }

    /// <summary>Makes the table temporal; <paramref name="buildAction" /> names its period columns and history table.</summary>
    public static TableBuilder<TEntity> IsTemporal<TEntity>(this TableBuilder<TEntity> tableBuilder, Action<TemporalTableBuilder<TEntity>> buildAction)
        where TEntity : class
    {
        buildAction(tableBuilder.IsTemporal());
        return tableBuilder;
    }
}

/// <summary>Names a temporal table's period columns and history table.</summary>
public class TemporalTableBuilder
{
    internal TemporalTableBuilder(IMutableEntityType entityType)
        => EntityType = entityType;

    internal IMutableEntityType EntityType { get; }

    /// <summary>The history table, in the table's schema (or the default schema); <c>&lt;table&gt;History</c> unless named.</summary>
    public TemporalTableBuilder UseHistoryTable(string name)
        => UseHistoryTable(name, null);

    /// <summary>The history table and its schema.</summary>
    public TemporalTableBuilder UseHistoryTable(string name, string? schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EntityType.SetAnnotation(SqlServerAnnotationNames.TemporalHistoryTableName, name);
        EntityType.SetAnnotation(SqlServerAnnotationNames.TemporalHistoryTableSchema, schema);
        return this;
    }

    /// <summary>The property (a shadow <see cref="DateTime" /> unless mapped) of the period's start; <c>PeriodStart</c> unless named.</summary>
    public TemporalPeriodPropertyBuilder HasPeriodStart(string propertyName)
        => Period(propertyName, SqlServerAnnotationNames.TemporalPeriodStartPropertyName, SqlServerAnnotationNames.TemporalPeriodStartColumnName);

    /// <summary>The property (a shadow <see cref="DateTime" /> unless mapped) of the period's end; <c>PeriodEnd</c> unless named.</summary>
    public TemporalPeriodPropertyBuilder HasPeriodEnd(string propertyName)
        => Period(propertyName, SqlServerAnnotationNames.TemporalPeriodEndPropertyName, SqlServerAnnotationNames.TemporalPeriodEndColumnName);

    private TemporalPeriodPropertyBuilder Period(string propertyName, string propertyAnnotation, string columnAnnotation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EntityType.SetAnnotation(propertyAnnotation, propertyName);
        return new TemporalPeriodPropertyBuilder(EntityType, columnAnnotation);
    }
}

/// <summary>Names a temporal table's period columns and history table.</summary>
public sealed class TemporalTableBuilder<TEntity> : TemporalTableBuilder
    where TEntity : class
{
    internal TemporalTableBuilder(IMutableEntityType entityType)
        : base(entityType)
    {
    }

    /// <inheritdoc cref="TemporalTableBuilder.UseHistoryTable(string)" />
    public new TemporalTableBuilder<TEntity> UseHistoryTable(string name)
        => (TemporalTableBuilder<TEntity>)base.UseHistoryTable(name);

    /// <inheritdoc cref="TemporalTableBuilder.UseHistoryTable(string, string?)" />
    public new TemporalTableBuilder<TEntity> UseHistoryTable(string name, string? schema)
        => (TemporalTableBuilder<TEntity>)base.UseHistoryTable(name, schema);
}

/// <summary>Configures a temporal table's period column.</summary>
public sealed class TemporalPeriodPropertyBuilder
{
    private readonly IMutableEntityType _entityType;
    private readonly string _columnAnnotation;

    internal TemporalPeriodPropertyBuilder(IMutableEntityType entityType, string columnAnnotation)
    {
        _entityType = entityType;
        _columnAnnotation = columnAnnotation;
    }

    /// <summary>The period column's name; the property's unless given.</summary>
    public TemporalPeriodPropertyBuilder HasColumnName(string? name)
    {
        _entityType.SetAnnotation(_columnAnnotation, name);
        return this;
    }

    /// <summary>The period column's fractional-second precision, from 0 to 7.</summary>
    public TemporalPeriodPropertyBuilder HasPrecision(int precision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(precision);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(precision, 7);
        _entityType.SetAnnotation(_columnAnnotation + "Precision", precision);
        return this;
    }
}

/// <summary>
///     Reads a temporal table's rows as they were (<c>FOR SYSTEM_TIME</c>), as Microsoft's provider does. The rows are read
///     from the table's columns, the hidden period columns included, and compose like any query; the times are UTC. Every
///     operation but <c>TemporalAsOf</c> returns several versions of a row, so its rows aren't tracked. Navigations read the
///     current rows of the tables they reach. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerDbSetExtensions.cs
/// </summary>
public static class SqlServerDbSetExtensions
{
    /// <summary>Every version of every row, current and historical: FOR SYSTEM_TIME ALL.</summary>
    public static IQueryable<TEntity> TemporalAll<TEntity>(this DbSet<TEntity> source)
        where TEntity : class
        => Temporal(source, "ALL").AsNoTracking();

    /// <summary>The rows as they were at <paramref name="utcPointInTime" />: FOR SYSTEM_TIME AS OF.</summary>
    public static IQueryable<TEntity> TemporalAsOf<TEntity>(this DbSet<TEntity> source, DateTime utcPointInTime)
        where TEntity : class
        => Temporal(source, "AS OF {0}", utcPointInTime);

    /// <summary>The versions that were current at some time from <paramref name="utcFrom" /> up to <paramref name="utcTo" />: FOR SYSTEM_TIME FROM ... TO.</summary>
    public static IQueryable<TEntity> TemporalFromTo<TEntity>(this DbSet<TEntity> source, DateTime utcFrom, DateTime utcTo)
        where TEntity : class
        => Temporal(source, "FROM {0} TO {1}", utcFrom, utcTo).AsNoTracking();

    /// <summary>The versions that were current at some time from <paramref name="utcFrom" /> to <paramref name="utcTo" /> inclusive: FOR SYSTEM_TIME BETWEEN.</summary>
    public static IQueryable<TEntity> TemporalBetween<TEntity>(this DbSet<TEntity> source, DateTime utcFrom, DateTime utcTo)
        where TEntity : class
        => Temporal(source, "BETWEEN {0} AND {1}", utcFrom, utcTo).AsNoTracking();

    /// <summary>The versions that began and ended within <paramref name="utcFrom" /> to <paramref name="utcTo" />: FOR SYSTEM_TIME CONTAINED IN.</summary>
    public static IQueryable<TEntity> TemporalContainedIn<TEntity>(this DbSet<TEntity> source, DateTime utcFrom, DateTime utcTo)
        where TEntity : class
        => Temporal(source, "CONTAINED IN ({0}, {1})", utcFrom, utcTo).AsNoTracking();

    // Identifiers are delimited and their braces doubled, since FromSqlRaw reads {n} as a parameter; times are parameters.
    private static IQueryable<TEntity> Temporal<TEntity>(DbSet<TEntity> source, string operation, params object[] times)
        where TEntity : class
    {
        var entityType = source.EntityType;
        if (entityType.GetRootType().FindAnnotation(SqlServerAnnotationNames.IsTemporal)?.Value is not true)
        {
            throw new InvalidOperationException(
                $"'{entityType.DisplayName()}' is not mapped to a temporal table; configure it with ToTable(t => t.IsTemporal()).");
        }

        var table = entityType.GetTableMappings().First().Table;
        var helper = source.GetService<ISqlGenerationHelper>();
        var columns = string.Join(", ", table.Columns.Select(c => Escape(helper.DelimitIdentifier(c.Name))));
        var sql = "SELECT " + columns + " FROM " + Escape(helper.DelimitIdentifier(table.Name, table.Schema)) + " FOR SYSTEM_TIME " + operation;
        return source.FromSqlRaw(sql, times);
    }

    private static string Escape(string identifier)
        => identifier.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
}

/// <summary>
///     A temporal table's period columns: shadow <see cref="DateTime" /> properties unless mapped, which the database sets on
///     every insert and update (so EF never writes them), with any column name and precision the model gave. Runs first, so
///     the properties get their type mappings. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerTemporalConvention.cs
/// </summary>
internal sealed class SqlServerTemporalConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes().Where(e => (e.BaseType == null) && IsTemporal(e)).ToList())
        {
            Period(entityType, SqlServerAnnotationNames.TemporalPeriodStartPropertyName, SqlServerAnnotationNames.TemporalPeriodStartColumnName, "PeriodStart");
            Period(entityType, SqlServerAnnotationNames.TemporalPeriodEndPropertyName, SqlServerAnnotationNames.TemporalPeriodEndColumnName, "PeriodEnd");
        }
    }

    internal static bool IsTemporal(IReadOnlyEntityType entityType)
        => entityType.FindAnnotation(SqlServerAnnotationNames.IsTemporal)?.Value is true;

    internal static string PeriodPropertyName(IReadOnlyEntityType entityType, bool start)
        => entityType.FindAnnotation(start ? SqlServerAnnotationNames.TemporalPeriodStartPropertyName : SqlServerAnnotationNames.TemporalPeriodEndPropertyName)?.Value as string
            ?? (start ? "PeriodStart" : "PeriodEnd");

    private static void Period(IConventionEntityType entityType, string propertyAnnotation, string columnAnnotation, string defaultName)
    {
        var name = entityType.FindAnnotation(propertyAnnotation)?.Value as string ?? defaultName;
        var property = entityType.Builder.Property(typeof(DateTime), name)
            ?? throw new InvalidOperationException($"The temporal period property '{name}' of '{entityType.DisplayName()}' must be a DateTime.");
        property.ValueGenerated(ValueGenerated.OnAddOrUpdate);
        if (entityType.FindAnnotation(columnAnnotation)?.Value is string column)
        {
            property.HasColumnName(column);
        }

        if (entityType.FindAnnotation(columnAnnotation + "Precision")?.Value is int precision)
        {
            property.HasPrecision(precision);
        }
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.adonet.Metadata;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     Carries the model's SQL Server options to the tables, columns, keys and indexes migrations see, under Microsoft's
///     annotation names; an identity column says its seed and increment as Microsoft's does ("1, 1"). Behaviour reference (not
///     copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Internal/SqlServerAnnotationProvider.cs
/// </summary>
internal sealed class SqlServerAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : AdoNetAnnotationProvider(dependencies)
{
    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {
        foreach (var annotation in base.For(table, designTime))
        {
            yield return annotation;
        }

        if (IsMemoryOptimized(table))
        {
            yield return new Annotation(SqlServerAnnotationNames.MemoryOptimized, true);
        }

        foreach (var annotation in Temporal(table))
        {
            yield return annotation;
        }

        if (TemporalEntityType(table) is { } entityType)
        {
            var store = StoreObjectIdentifier.Table(table.Name, table.Schema);
            yield return new Annotation(
                SqlServerAnnotationNames.TemporalPeriodStartColumnName,
                entityType.FindProperty(SqlServerTemporalConvention.PeriodPropertyName(entityType, start: true))?.GetColumnName(store));
            yield return new Annotation(
                SqlServerAnnotationNames.TemporalPeriodEndColumnName,
                entityType.FindProperty(SqlServerTemporalConvention.PeriodPropertyName(entityType, start: false))?.GetColumnName(store));
        }
    }

    /// <summary>A temporal table's history table, as a created, altered or dropped table carries it.</summary>
    internal static IEnumerable<IAnnotation> Temporal(ITable table)
    {
        if (TemporalEntityType(table) is not { } entityType)
        {
            yield break;
        }

        yield return new Annotation(SqlServerAnnotationNames.IsTemporal, true);
        yield return new Annotation(
            SqlServerAnnotationNames.TemporalHistoryTableName,
            entityType.FindAnnotation(SqlServerAnnotationNames.TemporalHistoryTableName)?.Value as string ?? table.Name + "History");
        yield return new Annotation(
            SqlServerAnnotationNames.TemporalHistoryTableSchema,
            entityType.FindAnnotation(SqlServerAnnotationNames.TemporalHistoryTableSchema)?.Value as string ?? table.Schema);
    }

    private static IEntityType? TemporalEntityType(ITable table)
        => table.EntityTypeMappings
            .Select(m => m.TypeBase)
            .OfType<IEntityType>()
            .Select(e => e.GetRootType())
            .FirstOrDefault(SqlServerTemporalConvention.IsTemporal);

    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        var properties = column.PropertyMappings.Select(m => m.Property).ToList();
        if (TemporalEntityType(column.Table) is { } temporal)
        {
            if (properties.Any(p => p.Name == SqlServerTemporalConvention.PeriodPropertyName(temporal, start: true)))
            {
                yield return new Annotation(SqlServerAnnotationNames.TemporalIsPeriodStartColumn, true);
            }

            if (properties.Any(p => p.Name == SqlServerTemporalConvention.PeriodPropertyName(temporal, start: false)))
            {
                yield return new Annotation(SqlServerAnnotationNames.TemporalIsPeriodEndColumn, true);
            }
        }

        if (properties.Any(p => p.FindAnnotation(SqlServerAnnotationNames.Sparse)?.Value is true))
        {
            yield return new Annotation(SqlServerAnnotationNames.Sparse, true);
        }

        if (IsIdentity(column))
        {
            var model = column.Table.Model.Model;
            var seed = properties.Select(p => p.FindAnnotation(SqlServerAnnotationNames.IdentitySeed)?.Value).OfType<long>().FirstOrDefault(
                model.FindAnnotation(SqlServerAnnotationNames.IdentitySeed)?.Value as long? ?? 1);
            var increment = properties.Select(p => p.FindAnnotation(SqlServerAnnotationNames.IdentityIncrement)?.Value).OfType<int>().FirstOrDefault(
                model.FindAnnotation(SqlServerAnnotationNames.IdentityIncrement)?.Value as int? ?? 1);
            yield return new Annotation(SqlServerAnnotationNames.Identity, string.Create(CultureInfo.InvariantCulture, $"{seed}, {increment}"));
        }
    }

    public override IEnumerable<IAnnotation> For(IUniqueConstraint constraint, bool designTime)
    {
        foreach (var annotation in base.For(constraint, designTime))
        {
            yield return annotation;
        }

        foreach (var annotation in Copied(constraint.MappedKeys, SqlServerAnnotationNames.Clustered, SqlServerAnnotationNames.FillFactor))
        {
            yield return annotation;
        }
    }

    public override IEnumerable<IAnnotation> For(ITableIndex index, bool designTime)
    {
        foreach (var annotation in base.For(index, designTime))
        {
            yield return annotation;
        }

        foreach (var annotation in Copied(
                     index.MappedIndexes,
                     SqlServerAnnotationNames.Clustered,
                     SqlServerAnnotationNames.FillFactor,
                     SqlServerAnnotationNames.CreatedOnline,
                     SqlServerAnnotationNames.SortInTempDb,
                     SqlServerAnnotationNames.DataCompression))
        {
            yield return annotation;
        }

        // Included properties by column name in this table.
        var modelIndex = index.MappedIndexes.First();
        if (modelIndex.FindAnnotation(SqlServerAnnotationNames.Include)?.Value is string[] includes)
        {
            var table = StoreObjectIdentifier.Table(index.Table.Name, index.Table.Schema);
            yield return new Annotation(
                SqlServerAnnotationNames.Include,
                includes.Select(name => ColumnName(modelIndex.DeclaringEntityType, name, table)).ToArray());
        }
    }

    internal static bool IsMemoryOptimized(ITable table)
        => table.EntityTypeMappings.Any(m => m.TypeBase.FindAnnotation(SqlServerAnnotationNames.MemoryOptimized)?.Value is true);

    private static IEnumerable<IAnnotation> Copied(IEnumerable<IAnnotatable> sources, params string[] names)
        => names
            .Select(name => sources.Select(s => s.FindAnnotation(name)).FirstOrDefault(a => a?.Value != null))
            .OfType<IAnnotation>()
            .Select(a => new Annotation(a.Name, a.Value));

    private static string ColumnName(IEntityType entityType, string propertyName, StoreObjectIdentifier table)
        => entityType.FindProperty(propertyName)?.GetColumnName(table)
            ?? throw new InvalidOperationException(
                $"Index on '{entityType.DisplayName()}' includes '{propertyName}', which is not a property mapped to a column of '{table.DisplayName()}'.");
}

/// <summary>
///     Resolves each key's numbering where the model chose one other than an IDENTITY column (<c>UseHiLo</c>,
///     <c>UseKeySequences</c>, <c>UseSequence</c>): the key is no identity, a sequence key defaults to the sequence's next value,
///     and the sequence is in the model. Runs after the core's identity convention. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerValueGenerationStrategyConvention.cs
/// </summary>
internal sealed class SqlServerValueGenerationConvention(ISqlGenerationHelper sqlGenerationHelper) : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        var model = modelBuilder.Metadata;
        var modelStrategy = model.FindAnnotation(SqlServerAnnotationNames.ValueGenerationStrategy)?.Value as SqlServerValueGenerationStrategy?;
        foreach (var property in model.GetEntityTypes().SelectMany(e => e.GetDeclaredProperties()).ToList())
        {
            var explicitStrategy = property.FindAnnotation(SqlServerAnnotationNames.ValueGenerationStrategy)?.Value as SqlServerValueGenerationStrategy?;
            var numberedByConvention = (property.FindAnnotation(AdoNetAnnotationNames.Identity) is { Value: true } identity)
                && (((IConventionAnnotation)identity).GetConfigurationSource() == ConfigurationSource.Convention);
            var strategy = explicitStrategy ?? (numberedByConvention ? modelStrategy : null);
            if (strategy == SqlServerValueGenerationStrategy.SequenceHiLo)
            {
                property.Builder.HasAnnotation(AdoNetAnnotationNames.Identity, false);
                property.Builder.HasAnnotation(
                    SqlServerAnnotationNames.HiLoSequenceName,
                    property.FindAnnotation(SqlServerAnnotationNames.HiLoSequenceName)?.Value
                        ?? model.FindAnnotation(SqlServerAnnotationNames.HiLoSequenceName)?.Value);
                property.Builder.HasAnnotation(
                    SqlServerAnnotationNames.HiLoSequenceSchema,
                    property.FindAnnotation(SqlServerAnnotationNames.HiLoSequenceSchema)?.Value
                        ?? model.FindAnnotation(SqlServerAnnotationNames.HiLoSequenceSchema)?.Value);
                continue;
            }

            if (strategy == SqlServerValueGenerationStrategy.Sequence)
            {
                UseSequence(modelBuilder, property);
            }
        }
    }

    private void UseSequence(IConventionModelBuilder modelBuilder, IConventionProperty property)
    {
        var model = modelBuilder.Metadata;
        var name = property.FindAnnotation(SqlServerAnnotationNames.SequenceName)?.Value as string
            ?? (property.DeclaringType.GetTableName() ?? property.DeclaringType.ShortName())
            + (model.FindAnnotation(SqlServerAnnotationNames.SequenceNameSuffix)?.Value as string ?? "Sequence");
        var schema = property.FindAnnotation(SqlServerAnnotationNames.SequenceSchema)?.Value as string
            ?? model.FindAnnotation(SqlServerAnnotationNames.SequenceSchema)?.Value as string;
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (model.FindSequence(name, schema) == null)
        {
            modelBuilder.HasSequence(name, schema)?.HasType(type);
        }

        property.Builder.HasAnnotation(AdoNetAnnotationNames.Identity, false);
        property.Builder.HasAnnotation(SqlServerAnnotationNames.SequenceName, name);
        property.Builder.HasAnnotation(SqlServerAnnotationNames.SequenceSchema, schema);
        property.Builder.HasDefaultValueSql("NEXT VALUE FOR " + sqlGenerationHelper.DelimitIdentifier(name, schema));
    }
}

/// <summary>
///     A memory-optimized table has no clustered index: its keys and indexes are nonclustered unless configured otherwise.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerMemoryOptimizedTablesConvention.cs
/// </summary>
internal sealed class SqlServerMemoryOptimizedTablesConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.FindAnnotation(SqlServerAnnotationNames.MemoryOptimized)?.Value is not true)
            {
                continue;
            }

            foreach (var key in entityType.GetDeclaredKeys())
            {
                key.Builder.HasAnnotation(SqlServerAnnotationNames.Clustered, key.FindAnnotation(SqlServerAnnotationNames.Clustered)?.Value ?? false);
            }

            foreach (var index in entityType.GetDeclaredIndexes())
            {
                index.Builder.HasAnnotation(SqlServerAnnotationNames.Clustered, index.FindAnnotation(SqlServerAnnotationNames.Clustered)?.Value ?? false);
            }
        }
    }
}

/// <summary>A dropped temporal table carries its history table, which is dropped with it.</summary>
// EF1001: as the core's provider, which this extends.
#pragma warning disable EF1001
internal sealed class SqlServerMigrationsAnnotationProvider(MigrationsAnnotationProviderDependencies dependencies)
    : AdoNetMigrationsAnnotationProvider(dependencies)
#pragma warning restore EF1001
{
    public override IEnumerable<IAnnotation> ForRemove(ITable table)
        => base.ForRemove(table).Concat(SqlServerAnnotationProvider.Temporal(table));
}

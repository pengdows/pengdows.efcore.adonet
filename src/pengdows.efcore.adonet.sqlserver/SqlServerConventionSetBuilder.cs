using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Metadata;

namespace pengdows.efcore.adonet.sqlserver;

internal sealed class SqlServerConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies,
    RelationalConventionSetBuilderDependencies relationalDependencies,
    IDatabaseCapabilities capabilities,
    ISqlGenerationHelper sqlGenerationHelper)
    : AdoNetConventionSetBuilder(dependencies, relationalDependencies, capabilities)
{
    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();
        conventionSet.ModelFinalizingConventions.Add(new SqlServerNullableUniqueIndexConvention(sqlGenerationHelper));
        conventionSet.ModelFinalizingConventions.Add(new SqlServerSingleCascadePathConvention());
        return conventionSet;
    }
}

/// <summary>
///     A unique index over nullable columns skips rows with nulls in them, as on other databases; SQL Server's own unique index
///     allows one such row. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerIndexConvention.cs
/// </summary>
internal sealed class SqlServerNullableUniqueIndexConvention(ISqlGenerationHelper sqlGenerationHelper) : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var index in modelBuilder.Metadata.GetEntityTypes().SelectMany(e => e.GetDeclaredIndexes()))
        {
            if (!index.IsUnique || index.GetFilter() != null)
            {
                continue;
            }

            // The column's nullability, not the property's: a derived type's column is nullable in its hierarchy's table.
            if (StoreObjectIdentifier.Create(index.DeclaringEntityType, StoreObjectType.Table) is not { } table)
            {
                continue;
            }

            var nullableColumns = index.Properties.Where(p => p.IsColumnNullable(table)).Select(p => p.GetColumnName(table)).Where(n => n != null).ToArray();
            if (nullableColumns.Length != 0)
            {
                index.Builder.HasFilter(string.Join(" AND ", nullableColumns.Select(n => $"{sqlGenerationHelper.DelimitIdentifier(n!)} IS NOT NULL")));
            }
        }
    }
}

/// <summary>
///     SQL Server refuses a foreign key that would give a table two paths of cascading deletes (or updates) from another, or
///     a cycle of them. Delete behaviours the model chose explicitly stay as they are; of those EF's conventions chose, each
///     that would make a second path or a cycle is cascaded by EF, on the rows it tracks, instead of the database
///     (ClientCascade, ClientSetNull). Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerOnDeleteConvention.cs
/// </summary>
internal sealed class SqlServerSingleCascadePathConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        // Foreign keys of one hierarchy that share columns share one constraint, so they're decided together.
        var constraints = modelBuilder.Metadata.GetEntityTypes()
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(fk => fk.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.SetNull)
            .Select(fk => (Key: fk, Principal: Table(fk.PrincipalEntityType), Dependent: Table(fk.DeclaringEntityType)))
            .Where(c => (c.Principal != null) && (c.Dependent != null) && !LinksRowsOfOneTable(c.Key, c.Principal, c.Dependent))
            .GroupBy(c => (c.Principal!, c.Dependent!, string.Join(",", c.Key.Properties.Select(p => p.GetColumnName()))))
            .OrderBy(g => g.Any(c => Explicit(c.Key)) ? 0 : 1)
            .ThenBy(g => g.Key.Item2, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Item3, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Item1, StringComparer.Ordinal)
            .ToList();

        var graph = new CascadeGraph();
        foreach (var constraint in constraints)
        {
            var (principal, dependent, _) = constraint.Key;
            if (constraint.Any(c => Explicit(c.Key)))
            {
                graph.Add(principal, dependent);
                continue;
            }

            if (graph.TryAdd(principal, dependent))
            {
                continue;
            }

            foreach (var (key, _, _) in constraint)
            {
                key.Builder.OnDelete(key.DeleteBehavior == DeleteBehavior.Cascade ? DeleteBehavior.ClientCascade : DeleteBehavior.ClientSetNull);
            }
        }
    }

    private static bool Explicit(IConventionForeignKey key)
        => key.GetDeleteBehaviorConfigurationSource() is ConfigurationSource.Explicit or ConfigurationSource.DataAnnotation;

    private static string? Table(IConventionEntityType entityType)
        => entityType.GetTableName() is { } name ? (entityType.GetSchema() ?? "") + "." + name : null;

    // Table splitting and owned types keep related rows in one table, linked by its key: no foreign key constraint.
    private static bool LinksRowsOfOneTable(IConventionForeignKey key, string? principal, string? dependent)
        => (principal == dependent) && key.IsUnique && key.DeclaringEntityType.FindPrimaryKey() is { } primaryKey
            && key.Properties.SequenceEqual(primaryKey.Properties);

    /// <summary>Tables joined by cascading foreign keys, kept free of cycles and of second paths between two tables.</summary>
    private sealed class CascadeGraph
    {
        private readonly Dictionary<string, HashSet<string>> _dependents = new(StringComparer.Ordinal);

        public void Add(string principal, string dependent)
        {
            if (!_dependents.TryGetValue(principal, out var dependents))
            {
                dependents = new HashSet<string>(StringComparer.Ordinal);
                _dependents[principal] = dependents;
            }

            dependents.Add(dependent);
        }

        // principal -> dependent makes a cycle where the dependent reaches the principal (a table referencing itself
        // included), and a second path where a table reaching the principal already reaches a table the dependent reaches.
        public bool TryAdd(string principal, string dependent)
        {
            var downstream = Reachable(dependent);
            if (downstream.Contains(principal))
            {
                return false;
            }

            foreach (var upstream in _dependents.Keys.Where(t => Reachable(t).Contains(principal)).Append(principal))
            {
                if (Reachable(upstream).Overlaps(downstream))
                {
                    return false;
                }
            }

            Add(principal, dependent);
            return true;
        }

        private HashSet<string> Reachable(string table)
        {
            var reached = new HashSet<string>(StringComparer.Ordinal) { table };
            var pending = new Stack<string>([table]);
            while (pending.TryPop(out var next))
            {
                foreach (var dependent in _dependents.GetValueOrDefault(next) ?? [])
                {
                    if (reached.Add(dependent))
                    {
                        pending.Push(dependent);
                    }
                }
            }

            return reached;
        }
    }
}

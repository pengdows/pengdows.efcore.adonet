using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Metadata;

namespace pengdows.efcore.adonet.sqlserver;

internal sealed class SqlServerConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies,
    RelationalConventionSetBuilderDependencies relationalDependencies,
    IDatabaseCapabilities capabilities)
    : AdoNetConventionSetBuilder(dependencies, relationalDependencies, capabilities)
{
    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();
        conventionSet.ModelFinalizingConventions.Add(new SqlServerNullableUniqueIndexConvention());
        return conventionSet;
    }
}

/// <summary>SQL Server requires filtered unique indexes for nullable keys.</summary>
internal sealed class SqlServerNullableUniqueIndexConvention : IModelFinalizingConvention
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

            var nullableColumns = index.Properties.Where(p => p.IsNullable).Select(p => p.GetColumnName()).Where(n => n != null).ToArray();
            if (nullableColumns.Length != 0)
            {
                index.Builder.HasFilter(string.Join(" AND ", nullableColumns.Select(n => $"[{n}] IS NOT NULL")));
            }
        }
    }
}

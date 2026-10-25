using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>
///     Relational conventions (table/column naming, keys, etc.), plus marking database-generated integer keys and fitting
///     names EF makes up to the database's identifier length.
/// </summary>
internal class AdoNetConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies,
    RelationalConventionSetBuilderDependencies relationalDependencies,
    IDatabaseCapabilities capabilities)
    : RelationalConventionSetBuilder(dependencies, relationalDependencies)
{
    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();
        conventionSet.ModelFinalizingConventions.Add(new AdoNetIdentityConvention());
        conventionSet.Add(new RelationalMaxIdentifierLengthConvention(capabilities.MaxIdentifierLength, Dependencies, RelationalDependencies));
        return conventionSet;
    }
}

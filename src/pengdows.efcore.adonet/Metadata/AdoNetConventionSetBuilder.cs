using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>Relational conventions (table/column naming, keys, etc.), plus marking database-generated integer keys.</summary>
public class AdoNetConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies,
    RelationalConventionSetBuilderDependencies relationalDependencies)
    : RelationalConventionSetBuilder(dependencies, relationalDependencies)
{
    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();
        conventionSet.ModelFinalizingConventions.Add(new AdoNetIdentityConvention());
        return conventionSet;
    }
}

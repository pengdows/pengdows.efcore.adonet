using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>Relational conventions (table/column naming, keys, etc.) with no provider-specific additions.</summary>
public class AdoNetConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies,
    RelationalConventionSetBuilderDependencies relationalDependencies)
    : RelationalConventionSetBuilder(dependencies, relationalDependencies);

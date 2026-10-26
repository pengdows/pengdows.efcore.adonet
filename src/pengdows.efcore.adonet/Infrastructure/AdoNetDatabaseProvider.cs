using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Infrastructure;

/// <summary>
///     Named for the dialect's assembly (a plugin's, or the core's for its own dialects), which is where EF's tools look for
///     the provider's design-time services: a plugin's know its dialect.
/// </summary>
internal sealed class AdoNetDatabaseProvider(DatabaseProviderDependencies dependencies, IAdoNetDialect dialect)
    : DatabaseProvider<AdoNetOptionsExtension>(dependencies)
{
    public override string Name { get; } = dialect.GetType().Assembly.GetName().Name!;
}

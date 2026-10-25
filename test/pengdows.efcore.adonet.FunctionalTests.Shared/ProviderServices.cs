using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.FunctionalTests;

/// <summary>The provider's services, for EF's test helpers, which ask for them by type.</summary>
public static class ProviderServices
{
    /// <summary>The provider's logging definitions (internal to it), resolved from its services.</summary>
    public static LoggingDefinitions LoggingDefinitions(IAdoNetDialect dialect)
        => new ServiceCollection().AddEntityFrameworkAdoNet(dialect).BuildServiceProvider().GetRequiredService<LoggingDefinitions>();
}

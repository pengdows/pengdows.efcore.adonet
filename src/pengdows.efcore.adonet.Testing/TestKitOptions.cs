using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.Testing;

internal static class TestKitOptions
{
    /// <summary>
    ///     Each distinct dialect and option set gets its own EF service provider, by design, and a provider's test project
    ///     uses many (dialect variants, server versions, options); EF treats more than twenty per process as an error unless
    ///     told otherwise, and fails whichever test happens to create the twenty-first.
    /// </summary>
    public static TBuilder AllowManyServiceProviders<TBuilder>(this TBuilder builder)
        where TBuilder : DbContextOptionsBuilder
    {
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        return builder;
    }
}

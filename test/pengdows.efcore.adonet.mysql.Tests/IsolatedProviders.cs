using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     EF counts the internal service providers it builds across a whole test run, and fails a context past twenty: a test of
///     many configurations (dialect variants, JSON options) builds its own here, one per configuration, from the options'
///     extensions, as EF would.
/// </summary>
internal static class IsolatedProviders
{
    private static readonly ConcurrentDictionary<string, IServiceProvider> Providers = new();

    public static DbContextOptionsBuilder<TContext> Isolated<TContext>(this DbContextOptionsBuilder<TContext> builder, string configuration)
        where TContext : DbContext
    {
        var options = builder.Options;
        var provider = Providers.GetOrAdd(
            configuration,
            _ =>
            {
                var services = new ServiceCollection();
                foreach (var extension in options.Extensions)
                {
                    extension.ApplyServices(services);
                }

                return services.BuildServiceProvider();
            });
        return builder.UseInternalServiceProvider(provider);
    }

    public static DbContextOptionsBuilder Isolated(this DbContextOptionsBuilder builder, string configuration)
    {
        var options = builder.Options;
        var provider = Providers.GetOrAdd(
            configuration,
            _ =>
            {
                var services = new ServiceCollection();
                foreach (var extension in options.Extensions)
                {
                    extension.ApplyServices(services);
                }

                return services.BuildServiceProvider();
            });
        return builder.UseInternalServiceProvider(provider);
    }
}

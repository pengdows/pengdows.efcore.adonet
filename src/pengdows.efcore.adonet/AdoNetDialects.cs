using System.Collections.Concurrent;
using System.Data.Common;

namespace pengdows.efcore.adonet;

/// <summary>
///     Which dialect to use when only a provider factory is given. Dialect packages register themselves here; the core knows
///     none, so a factory nothing registered for needs <c>UseDialect</c>.
/// </summary>
public static class AdoNetDialects
{
    private static readonly ConcurrentDictionary<Type, IAdoNetDialect> Registered = new();

    public static void Register<TFactory>(IAdoNetDialect dialect)
        where TFactory : DbProviderFactory
        => Register(typeof(TFactory), dialect);

    public static void Register(Type factoryType, IAdoNetDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(factoryType);
        ArgumentNullException.ThrowIfNull(dialect);

        Registered[factoryType] = dialect;
    }

    /// <summary>The dialect registered for <paramref name="factory" />'s type, or null.</summary>
    public static IAdoNetDialect? Find(DbProviderFactory? factory)
        => (factory != null) && Registered.TryGetValue(factory.GetType(), out var dialect) ? dialect : null;
}

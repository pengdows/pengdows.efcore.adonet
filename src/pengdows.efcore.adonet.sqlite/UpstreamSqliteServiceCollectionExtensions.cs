using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Microsoft's <c>AddEntityFrameworkSqlite</c>: the SQLite provider's services, over the core registration.</summary>
public static class SqliteServiceCollectionExtensions
{
    public static IServiceCollection AddEntityFrameworkSqlite(this IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkAdoNet(pengdows.efcore.adonet.sqlite.SqliteDialect.Instance);

    /// <summary>Registers <typeparamref name="TContext" /> as a service, configured to connect to SQLite.</summary>
    public static IServiceCollection AddSqlite<TContext>(
        this IServiceCollection serviceCollection,
        string? connectionString,
        Action<pengdows.efcore.adonet.Infrastructure.AdoNetDbContextOptionsBuilder>? sqliteOptionsAction = null,
        Action<DbContextOptionsBuilder>? optionsAction = null)
        where TContext : DbContext
        => serviceCollection.AddDbContext<TContext>((_, options) =>
        {
            optionsAction?.Invoke(options);
            Action<pengdows.efcore.adonet.Infrastructure.AdoNetDbContextOptionsBuilder> configure = o => sqliteOptionsAction?.Invoke(new Microsoft.EntityFrameworkCore.Infrastructure.SqliteDbContextOptionsBuilder(o));
            if (connectionString is null)
            {
                options.UseSqlite(o => configure(o));
            }
            else
            {
                options.UseSqlite(connectionString, configure);
            }
        });
}

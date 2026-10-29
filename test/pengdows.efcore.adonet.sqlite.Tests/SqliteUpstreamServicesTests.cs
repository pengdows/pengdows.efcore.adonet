using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     Microsoft's <c>AddEntityFrameworkSqlite</c>: the SQLite provider's services are registered, so a context resolves the
///     SQLite dialect from the application's service provider.
/// </summary>
public class SqliteUpstreamServicesTests
{
    [Fact]
    public void AddEntityFrameworkSqlite_registers_the_sqlite_dialect()
    {
        using var services = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

        Assert.Same(SqliteDialect.Instance, services.GetRequiredService<IAdoNetDialect>());
    }

    [Fact]
    public void AddEntityFrameworkSqlite_returns_the_same_collection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddEntityFrameworkSqlite());
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     Microsoft's <c>AddSqlite&lt;TContext&gt;</c> (a context registered with the SQLite options) and <c>IsSqlite</c> (whether a
///     context's dialect is SQLite's).
/// </summary>
public class SqliteUpstreamContextTests
{
    private const string ConnectionString = "Data Source=:memory:";

    [Fact]
    public void AddSqlite_registers_a_context_that_uses_the_sqlite_dialect()
    {
        var services = new ServiceCollection().AddSqlite<BlogContext>(ConnectionString);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<BlogContext>();

        Assert.Same(SqliteDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void AddSqlite_runs_the_options_action_before_the_sqlite_options()
    {
        var order = new List<string>();
        var services = new ServiceCollection().AddSqlite<BlogContext>(
            ConnectionString,
            sqliteOptionsAction: _ => order.Add("sqlite"),
            optionsAction: _ => order.Add("options"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<BlogContext>();

        Assert.Equal(["options", "sqlite"], order);
    }

    [Fact]
    public void AddSqlite_returns_the_same_collection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddSqlite<BlogContext>(ConnectionString));
    }

    [Fact]
    public void IsSqlite_is_true_for_a_sqlite_context()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)).UseSqlite(ConnectionString).Options);

        Assert.True(context.Database.IsSqlite());
    }

    [Fact]
    public void IsSqlite_is_false_for_another_providers_context()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)).UseAdoNet(new fakeDbFactory(SupportedDatabase.PostgreSql), "Data Source=fake;EmulatedProduct=PostgreSql", AdoNetDialect.Ansi).Options);

        Assert.False(context.Database.IsSqlite());
    }
}

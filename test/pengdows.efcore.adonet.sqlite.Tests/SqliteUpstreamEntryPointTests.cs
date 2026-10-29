using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     Microsoft's own entry points (UseSqlite on DbContextOptionsBuilder and its generic), which every EF SQLite provider
///     exposes: the same forms as UseAdoNetSqlite, and the same dialect.
/// </summary>
public class SqliteUpstreamEntryPointTests
{
    private const string ConnectionString = "Data Source=:memory:";

    private static DbContextOptionsBuilder<BlogContext> Builder()
        => new DbContextOptionsBuilder<BlogContext>().ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning));

    [Fact]
    public void UseSqlite_on_a_connection_string_uses_the_sqlite_dialect()
    {
        using var context = new BlogContext(Builder().UseSqlite(ConnectionString).Options);

        Assert.Same(SqliteDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void UseSqlite_on_a_connection_uses_the_sqlite_dialect_and_keeps_the_connection()
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=Sqlite" };
        using var context = new BlogContext(Builder().UseSqlite(connection).Options);

        Assert.Same(SqliteDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Same(connection, context.Database.GetDbConnection());
    }

    [Fact]
    public void UseSqlite_with_options_only_names_the_sqlite_dialect_for_a_connection_set_later()
    {
        using var context = new BlogContext(Builder().UseSqlite(o => o.CommandTimeout(5)).Options);
        context.Database.SetConnectionString(ConnectionString);

        Assert.Same(SqliteDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Equal(5, context.Database.GetCommandTimeout());
    }

    [Fact]
    public void Non_generic_UseSqlite_configures_the_same_provider()
    {
        var builder = Builder();
        ((DbContextOptionsBuilder)builder).UseSqlite(ConnectionString);

        using var context = new BlogContext(builder.Options);
        Assert.Same(SqliteDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void UseSqlite_passes_the_microsoft_options_builder_to_its_action()
    {
        SqliteDbContextOptionsBuilder? seen = null;
        using var context = new BlogContext(Builder().UseSqlite(ConnectionString, o => seen = o).Options);

        Assert.NotNull(seen);
        Assert.Same(SqliteDialect.Instance, context.GetService<IAdoNetDialect>());
    }
}

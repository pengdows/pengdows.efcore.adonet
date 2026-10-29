using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     Npgsql's own entry points (Npgsql.EntityFrameworkCore.PostgreSQL's UseNpgsql, on DbContextOptionsBuilder and its generic),
///     which every EF PostgreSQL provider exposes: the same four forms as UseAdoNetPostgreSql, and the same dialect.
/// </summary>
public class PostgreSqlUpstreamEntryPointTests
{
    private const string ConnectionString = "Host=localhost";

    // Each case makes its own service provider; EF warns past twenty per process.
    private static DbContextOptionsBuilder<BlogContext> Builder()
        => new DbContextOptionsBuilder<BlogContext>().ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning));

    [Fact]
    public void UseNpgsql_on_a_connection_string_uses_the_postgresql_dialect()
    {
        using var context = new BlogContext(Builder().UseNpgsql(ConnectionString).Options);

        Assert.Same(PostgreSqlDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void UseNpgsql_on_a_connection_uses_the_postgresql_dialect_and_keeps_the_connection()
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=PostgreSql" };
        using var context = new BlogContext(Builder().UseNpgsql(connection).Options);

        Assert.Same(PostgreSqlDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Same(connection, context.Database.GetDbConnection());
    }

    [Fact]
    public void UseNpgsql_on_a_data_source_uses_the_postgresql_dialect()
    {
        using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        using var context = new BlogContext(Builder().UseNpgsql(dataSource).Options);

        Assert.Same(PostgreSqlDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void UseNpgsql_with_options_only_names_the_postgresql_dialect_for_a_connection_set_later()
    {
        using var context = new BlogContext(Builder().UseNpgsql(o => o.CommandTimeout(5)).Options);
        context.Database.SetConnectionString(ConnectionString);

        Assert.Same(PostgreSqlDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Equal(5, context.Database.GetCommandTimeout());
    }

    [Fact]
    public void Non_generic_UseNpgsql_configures_the_same_provider()
    {
        var builder = Builder();
        ((DbContextOptionsBuilder)builder).UseNpgsql(ConnectionString);

        using var context = new BlogContext(builder.Options);
        Assert.Same(PostgreSqlDialect.Instance, context.GetService<IAdoNetDialect>());
    }
}

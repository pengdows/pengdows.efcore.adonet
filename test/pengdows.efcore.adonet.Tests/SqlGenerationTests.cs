using DuckDB.NET.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.efcore.adonet.Dialects;
using pengdows.flatfile;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Dialect-driven SQL shape, checked without touching a database.</summary>
public class SqlGenerationTests
{
    private static BlogContext Context(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        configure(builder);
        return new BlogContext(builder.Options);
    }

    private static string PagedQuery(BlogContext context)
    {
        var name = "x";
        return context.Blogs.Where(b => b.Name == name).OrderBy(b => b.Id).Skip(5).Take(10).ToQueryString();
    }

    [Fact]
    public void Ansi_uses_offset_fetch_and_at_parameters()
    {
        using var context = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", o => o.UseDialect(AdoNetSqlDialect.Ansi)));

        var sql = PagedQuery(context);

        Assert.Contains("WHERE \"b\".\"Name\" = @name", sql);
        Assert.Matches(@"OFFSET @p\w* ROWS FETCH NEXT @p\w* ROWS ONLY", sql);
    }

    [Fact]
    public void Sqlite_uses_limit_offset()
    {
        using var context = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:"));

        var sql = PagedQuery(context);

        Assert.Matches(@"LIMIT @p\w*\s+OFFSET @p\w*", sql);
    }

    [Fact]
    public void Sqlite_offset_only_emits_no_limit_value()
    {
        using var context = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:"));

        var sql = context.Blogs.OrderBy(b => b.Id).Skip(5).ToQueryString();

        Assert.Contains("LIMIT -1", sql);
    }

    [Fact]
    public void DuckDb_uses_dollar_parameters()
    {
        using var context = Context(b => b.UseAdoNet(DuckDBClientFactory.Instance, "Data Source=:memory:"));

        var sql = PagedQuery(context);

        Assert.Contains("= $name", sql);
        Assert.Matches(@"LIMIT \$p\w*\s+OFFSET \$p\w*", sql);
    }

    [Fact]
    public void FlatFile_uses_colon_parameters_and_literal_paging()
    {
        using var context = Context(b => b.UseAdoNet(FlatFileProviderFactory.Instance, "path=/nonexistent"));

        var sql = PagedQuery(context);

        Assert.Contains("= :name", sql);
        Assert.Contains("OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY", sql);
    }

    [Fact]
    public void Bool_constants_use_dialect_literals()
    {
        using var context = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:"));

        var sql = context.Blogs.Select(b => new { b.Id, Flag = true }).ToQueryString();

        Assert.Contains("TRUE AS \"Flag\"", sql);
    }

    [Fact]
    public void Schemas_are_dropped_when_unsupported()
    {
        using var context = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:"));
        var helper = context.GetService<Microsoft.EntityFrameworkCore.Storage.ISqlGenerationHelper>();

        Assert.Equal("\"T\"", helper.DelimitIdentifier("T", "dbo"));
    }

    [Fact]
    public void Same_dialect_instance_reuses_the_internal_service_provider()
    {
        // EF throws after 20 internal service providers when this warning is configured to throw.
        for (var i = 0; i < 30; i++)
        {
            using var context = Context(b => b
                .UseAdoNet(SqliteFactory.Instance, $"Data Source=db{i};Mode=Memory")
                .ConfigureWarnings(w => w.Throw(CoreEventId.ManyServiceProvidersCreatedWarning)));

            Assert.NotNull(context.Model);
        }
    }

    [Fact]
    public void Configuring_two_connection_sources_is_rejected()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = Context(b => b
            .UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:")
            .UseAdoNet(connection));

        Assert.Throws<InvalidOperationException>(() => context.Database.GetDbConnection());
    }

    [Fact]
    public void Positional_parameter_dialects_are_rejected()
    {
        Assert.Throws<NotSupportedException>(
            () => Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", o => o.UseDialect(new PositionalDialect()))));
    }

    [Fact]
    public void Connection_and_data_source_overloads_detect_the_dialect()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var c1 = Context(b => b.UseAdoNet(connection));
        Assert.Contains("LIMIT", c1.Blogs.Take(1).ToQueryString());

        using var dataSource = DuckDBClientFactory.Instance.CreateDataSource("Data Source=:memory:");
        using var c2 = Context(b => b.UseAdoNet(dataSource));
        Assert.Contains("LIMIT $p", c2.Blogs.Take(1).ToQueryString());
    }

    private sealed class PositionalDialect : AdoNetSqlDialect
    {
        public override string ParameterMarker => "";
    }
}

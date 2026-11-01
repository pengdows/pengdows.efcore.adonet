using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     Npgsql's jsonb functions on <c>EF.Functions</c>: containment (<c>@&gt;</c>, <c>&lt;@</c>), key existence (<c>?</c>,
///     <c>?|</c>, <c>?&amp;</c>) and <c>jsonb_typeof</c>, over a jsonb column, or text taken as jsonb. Provider-only: the operators
///     are PostgreSQL's. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlJsonDbFunctionsTranslator.cs
/// </summary>
public sealed class PostgreSqlJsonFunctionTests
{
    private sealed class Row
    {
        public int Id { get; set; }
        public string Document { get; set; } = "{}";
        public string Text { get; set; } = "{}";
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().Property(r => r.Document).HasColumnType("jsonb");
    }

    private static Context Create()
        => new(new DbContextOptionsBuilder<Context>().UseAdoNetPostgreSql("Host=localhost").Options);

    [Fact]
    public void JsonContains_is_the_containment_operator_with_the_other_side_taken_as_jsonb()
    {
        using var context = Create();
        var fragment = "{\"a\":1}";

        var sql = context.Rows.Where(r => EF.Functions.JsonContains(r.Document, fragment)).ToQueryString();

        Assert.Contains("r.\"Document\" @> CAST(@fragment AS jsonb)", sql);
    }

    [Fact]
    public void JsonContained_is_the_contained_by_operator()
    {
        using var context = Create();

        var sql = context.Rows.Where(r => EF.Functions.JsonContained("{\"a\":1}", r.Document)).ToQueryString();

        Assert.Contains("CAST('{\"a\":1}' AS jsonb) <@ r.\"Document\"", sql);
    }

    [Fact]
    public void A_text_column_is_cast_to_jsonb()
    {
        using var context = Create();

        var sql = context.Rows.Where(r => EF.Functions.JsonContains(r.Document, r.Text)).ToQueryString();

        Assert.Contains("r.\"Document\" @> CAST(r.\"Text\" AS jsonb)", sql);
    }

    [Fact]
    public void JsonExists_tests_a_key()
    {
        using var context = Create();

        var sql = context.Rows.Where(r => EF.Functions.JsonExists(r.Document, "it's")).ToQueryString();

        Assert.Contains("r.\"Document\" ? 'it''s'", sql);
    }

    [Fact]
    public void A_keys_parameter_binds_as_a_text_array()
    {
        using var context = Create();
        string[] keys = ["a", "it's"];

        var sql = context.Rows.Where(r => EF.Functions.JsonExistAll(r.Document, keys)).ToQueryString();

        Assert.DoesNotContain("jsonb_array_elements", sql);
        Assert.Matches(@"\?& (@keys|ARRAY\['a','it''s'\]::text\[\])", sql);
    }

    [Fact]
    public void JsonTypeof_is_jsonb_typeof()
    {
        using var context = Create();

        var sql = context.Rows.Select(r => EF.Functions.JsonTypeof(r.Document)).ToQueryString();

        Assert.Contains("jsonb_typeof(r.\"Document\")", sql);
    }

    [Fact]
    public void Json_functions_run_only_in_queries()
    {
        Assert.Throws<InvalidOperationException>(() => EF.Functions.JsonContains("{}", "{}"));
    }
}

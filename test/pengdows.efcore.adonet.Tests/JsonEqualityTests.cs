using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A JSON column compared with JSON text: a bare string is a JSON string scalar to some databases, which equals no
///     document, so the text goes through the dialect's text-to-JSON conversion first. A column that stores its document as
///     plain text compares as text and takes no conversion.
/// </summary>
public class JsonEqualityTests
{
    public class Row
    {
        public int Id { get; set; }
        public string Body { get; set; } = "";
        public string Other { get; set; } = "";
    }

    private sealed class NativeContext(DbContextOptions<NativeContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().Property(r => r.Body).HasColumnType("JSON");
    }

    private sealed class TextContext(DbContextOptions<TextContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().Property(r => r.Body).HasColumnType("nvarchar(max)");
    }

    private static readonly IAdoNetDialect TextDialect = new PartsDialect(
        "Json stored as text",
        new PartsSql(json: new JsonSyntax(
            "nvarchar(max)",
            "JSON_VALUE({0}, {1})",
            "JSON_QUERY({0}, {1})",
            "CONVERT({2}, JSON_VALUE({0}, {1}))",
            booleanValue: null,
            binaryValue: null,
            "CONCAT({0}, {1})",
            "JSON_MODIFY({0}, {1}, {2})",
            "JSON_QUERY({0})")
        {
            StoresDocumentAsText = true
        }));

    private static string Native(Func<NativeContext, IQueryable<Row>> query)
    {
        using var context = new FakeDatabase().ContextOf<NativeContext>(TestDialects.Json, o => new NativeContext(o));
        return query(context).ToQueryString().ReplaceLineEndings("\n");
    }

    private static string AsText(Func<TextContext, IQueryable<Row>> query)
    {
        using var context = new FakeDatabase().ContextOf<TextContext>(TextDialect, o => new TextContext(o));
        return query(context).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void A_constant_is_converted_to_json_before_it_is_compared()
        => Assert.Contains(@"WHERE ""r"".""Body"" = JSON('[1,10]')", Native(c => c.Rows.Where(r => r.Body == "[1,10]")));

    [Fact]
    public void A_parameter_is_converted_to_json_before_it_is_compared()
    {
        var text = "[1,10]";

        Assert.Matches(@"WHERE ""r"".""Body"" = JSON\(@\w+\)", Native(c => c.Rows.Where(r => r.Body == text)));
    }

    [Fact]
    public void Not_equal_converts_the_text_the_same_way()
        => Assert.Contains(@"WHERE ""r"".""Body"" <> JSON('{}')", Native(c => c.Rows.Where(r => r.Body != "{}")));

    [Fact]
    public void The_text_is_converted_whichever_side_it_is_on()
        => Assert.Contains(@"JSON('[2]') = ""r"".""Body""", Native(c => c.Rows.Where(r => "[2]" == r.Body)));

    [Fact]
    public void Text_that_looks_like_a_sql_injection_is_converted_as_a_literal_not_as_sql()
        => Assert.Contains(@"""r"".""Body"" = JSON('[""x'' OR 1=1 --""]')", Native(c => c.Rows.Where(r => r.Body == "[\"x' OR 1=1 --\"]")));

    [Fact]
    public void A_column_compared_with_a_column_is_not_converted()
        => Assert.DoesNotContain("JSON(", Native(c => c.Rows.Where(r => r.Body == r.Other)));

    [Fact]
    public void A_column_that_is_not_json_is_not_converted()
        => Assert.DoesNotContain("JSON(", Native(c => c.Rows.Where(r => r.Other == "[1]")));

    [Fact]
    public void A_document_stored_as_text_compares_as_text()
        => Assert.Contains(@"WHERE ""r"".""Body"" = '[1,10]'", AsText(c => c.Rows.Where(r => r.Body == "[1,10]")));

    [Fact]
    public void A_dialect_with_no_json_support_leaves_the_comparison_alone()
    {
        using var context = new FakeDatabase().ContextOf<NativeContext>(TestDialects.Ansi, o => new NativeContext(o));

        Assert.Contains(@"WHERE ""r"".""Body"" = '[1]'", context.Rows.Where(r => r.Body == "[1]").ToQueryString());
    }
}

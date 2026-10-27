using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Owned and complex types mapped to a JSON column (ToJson): the column takes the dialect's JSON type, a property inside
///     it is read with the dialect's JSON functions, and changing one property patches the document in place.
/// </summary>
/// <remarks>Behaviour reference (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqliteQuerySqlGenerator.cs</remarks>
public class JsonTests
{
    public class Doc
    {
        public int Id { get; set; }
        public Detail Detail { get; set; } = new();
    }

    public class Detail
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public bool Flag { get; set; }
        public string Spaced { get; set; } = "";
        public string Hostile { get; set; } = "";
        public int Digit { get; set; }
        public int Underscored { get; set; }
        public List<sbyte> Signed { get; set; } = [];
    }

    public class ComplexDoc
    {
        public int Id { get; set; }
        public Detail Detail { get; set; } = new();
    }

    private class RawDoc
    {
        public int Id { get; set; }
        public string Document { get; set; } = "";
    }

    private sealed class DocContext(DbContextOptions<DocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();
        public DbSet<ComplexDoc> ComplexDocs => Set<ComplexDoc>();
        public DbSet<RawDoc> RawDocs => Set<RawDoc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Doc>().OwnsOne(d => d.Detail, b =>
            {
                b.ToJson();
                b.Property(d => d.Spaced).HasJsonPropertyName("Full name");
                b.Property(d => d.Hostile).HasJsonPropertyName("a\"b\\c");
                b.Property(d => d.Digit).HasJsonPropertyName("1st");
                b.Property(d => d.Underscored).HasJsonPropertyName("_a_1");
            });
            modelBuilder.Entity<ComplexDoc>().ComplexProperty(d => d.Detail, b =>
            {
                b.ToJson();
                b.Property(d => d.Spaced).HasJsonPropertyName("Full name");
            });
            modelBuilder.Entity<RawDoc>().Property(d => d.Document).HasColumnType("JSON");
        }
    }

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private static DocContext Context(FakeDatabase db)
        => db.ContextOf<DocContext>(TestDialects.Json, o => new DocContext(o));

    [Fact]
    public void The_json_column_takes_the_dialects_json_type()
    {
        using var context = Context(new FakeDatabase());

        Assert.Contains("\"Detail\" JSON", context.Database.GenerateCreateScript());
    }

    [Fact]
    public void A_string_inside_the_document_is_read_with_the_value_function()
    {
        using var context = Context(new FakeDatabase());

        Assert.Contains("WHERE JSON_VALUE(\"d\".\"Detail\", '$.Name') = 'x'", context.Docs.Where(d => d.Detail.Name == "x").ToQueryString());
    }

    [Fact]
    public void A_typed_value_inside_the_document_is_returned_as_its_type()
    {
        using var context = Context(new FakeDatabase());

        Assert.Contains("WHERE JSON_VALUE(\"d\".\"Detail\", '$.Count' RETURNING INTEGER) = 3", context.Docs.Where(d => d.Detail.Count == 3).ToQueryString());
    }

    [Fact]
    public async Task Changing_one_property_patches_the_document()
    {
        var db = new FakeDatabase();
        await using var context = Context(db);
        var doc = new Doc { Id = 1, Detail = new Detail { Name = "a" } };
        context.Attach(doc);

        doc.Detail.Name = "b";
        await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(Ct)); // fakeDb reports no row updated

        Assert.Contains("\"Detail\" = JSON_SET(\"Detail\", '$.Name', @p", db.Commands[0]);
    }

    // A bool bound as a parameter would land in the document as 1 or 0: it goes as the text true or false, made JSON.
    [Fact]
    public async Task A_boolean_is_patched_in_as_json_true_or_false()
    {
        var db = new FakeDatabase();
        await using var context = Context(db);
        var doc = new Doc { Id = 1, Detail = new Detail { Name = "a" } };
        context.Attach(doc);

        doc.Detail.Flag = true;
        await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(Ct));

        Assert.Contains("\"Detail\" = JSON_SET(\"Detail\", '$.Flag', JSON(@p", db.Commands[0]);
        Assert.Contains("'true'", db.Parameters[0]);
    }

    // A member name that isn't a plain identifier is a quoted path member, however the path is used.
    [Fact]
    public void A_member_name_with_a_space_is_a_quoted_path_member()
    {
        using var context = Context(new FakeDatabase());

        Assert.Contains("JSON_VALUE(\"d\".\"Detail\", '$.\"Full name\"') = 'x'", context.Docs.Where(d => d.Detail.Spaced == "x").ToQueryString());
    }

    // The path is delimited by the provider's own JSON path rule (EF's SqlGenerationHelper.DelimitJsonPathElement), so a
    // provider whose path grammar differs overrides one place.
    [Fact]
    public void A_member_name_with_a_quote_and_a_backslash_is_json_encoded()
    {
        using var context = Context(new FakeDatabase());

        Assert.Contains("JSON_VALUE(\"d\".\"Detail\", '$.\"a\\u0022b\\\\c\"') = 'x'", context.Docs.Where(d => d.Detail.Hostile == "x").ToQueryString());
    }

    // Bare, a path member is an identifier: a letter or underscore first, then letters, digits and underscores.
    [Fact]
    public void A_member_name_is_bare_only_when_it_reads_as_an_identifier()
    {
        using var context = Context(new FakeDatabase());

        Assert.Contains("'$.\"1st\"' RETURNING INTEGER) = 1", context.Docs.Where(d => d.Detail.Digit == 1).ToQueryString());
        Assert.Contains("'$._a_1' RETURNING INTEGER) = 1", context.Docs.Where(d => d.Detail.Underscored == 1).ToQueryString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Comparing_a_json_document_with_text_converts_the_text_to_json(bool notEqual)
    {
        using var context = Context(new FakeDatabase());
        var text = "{\"name\":\"x\"}";
        var query = notEqual
            ? context.RawDocs.Where(d => d.Document != text)
            : context.RawDocs.Where(d => d.Document == text);

        var sql = query.ToQueryString();

        Assert.Contains(notEqual ? " <> JSON(" : " = JSON(", sql);
    }

    [Fact]
    public async Task A_member_patched_by_a_bulk_update_is_named_by_the_same_path_a_read_uses()
    {
        var db = new FakeDatabase();
        await using var context = Context(db);

        await context.ComplexDocs.ExecuteUpdateAsync(s => s.SetProperty(d => d.Detail.Spaced, "b"), Ct);

        Assert.Contains("JSON_SET(\"c\".\"Detail\", '$.\"Full name\"', ", db.Commands[0]);
    }

    [Fact]
    public async Task A_signed_byte_json_collection_is_not_written_as_base64()
    {
        var db = new FakeDatabase();
        await using var context = Context(db);
        var doc = new Doc { Id = 1, Detail = new Detail { Signed = [1, 2] } };
        context.Attach(doc);
        doc.Detail.Signed = [3, -4];

        await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(Ct));

        Assert.Contains("JSON_SET", db.Commands[0]);
        Assert.DoesNotContain("base64", db.Commands[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[3,-4]", db.Parameters[0], StringComparison.Ordinal);
    }
}

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
    }

    private sealed class DocContext(DbContextOptions<DocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Doc>().OwnsOne(d => d.Detail, b => b.ToJson());
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
}

using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>Parity probe for SQLite JSON owned/complex mapping in the fakeDb lane.</summary>
public sealed class SqliteUpstreamGapFakeDbTests
{
    [Fact]
    public void Json_owned_property_can_be_projected_through_fake_sqlite()
    {
        var db = new FakeSqlite();
        using var context = db.Context<DocumentContext>(options => new DocumentContext(options));
        db.Returns(["Oslo"]);

        var value = context.Documents.Select(document => document.Details.Name).Single();

        Assert.Equal("Oslo", value);
        Assert.Contains("json_extract", Assert.Single(db.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decimal_columns_use_sqlite_text_storage_for_exact_arithmetic()
    {
        using var context = new DecimalContext(new DbContextOptionsBuilder<DecimalContext>()
            .UseAdoNetSqlite("Data Source=fake")
            .Options);

        Assert.Contains("TEXT", context.Database.GenerateCreateScript(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Document
    {
        public int Id { get; set; }
        public Details Details { get; set; } = new();
    }

    private sealed class Details
    {
        public string Name { get; set; } = "";
    }

    private sealed class DocumentContext(DbContextOptions<DocumentContext> options) : DbContext(options)
    {
        public DbSet<Document> Documents => Set<Document>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Document>().OwnsOne(document => document.Details, owned => owned.ToJson());
    }

    private sealed class DecimalRow
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class DecimalContext(DbContextOptions<DecimalContext> options) : DbContext(options)
    {
        public DbSet<DecimalRow> Rows => Set<DecimalRow>();
    }
}

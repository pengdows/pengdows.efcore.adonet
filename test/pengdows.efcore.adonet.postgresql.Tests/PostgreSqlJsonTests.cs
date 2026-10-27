using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.postgresql.Tests;

public sealed class PostgreSqlJsonTests
{
    private sealed class Document
    {
        public int Id { get; set; }
        public Details Details { get; set; } = new();
    }

    private sealed class Details
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Document> Documents => Set<Document>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Document>().OwnsOne(d => d.Details, owned => owned.ToJson());
    }

    private static Context CreateContext()
    {
        var options = new DbContextOptionsBuilder<Context>()
            .UseAdoNetPostgreSql("Host=localhost")
            .Options;
        return new Context(options);
    }

    [Fact]
    public void Uses_jsonb_for_json_documents()
    {
        using var context = CreateContext();

        Assert.Contains("\"Details\" jsonb", context.Database.GenerateCreateScript(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Uses_jsonpath_for_scalar_reads()
    {
        using var context = CreateContext();

        var sql = context.Documents.Where(d => d.Details.Name == "x" && d.Details.Count == 3).ToQueryString();

        Assert.Contains("jsonb_path_query_first", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AS jsonpath", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AS INTEGER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Uses_typed_json_array_for_parameterized_collections()
    {
        using var context = CreateContext();
        int[] ids = [];

        var sql = context.Documents
            .Join(ids, document => document.Id, id => id, (document, _) => document)
            .ToQueryString();

        Assert.Contains("jsonb_array_elements", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CAST(e.value #>> '{}' AS integer)", sql, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class PostgreSqlJsonIntegrationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private sealed class RawDocument
    {
        public int Id { get; set; }
        public string Document { get; set; } = "";
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<RawDocument> Documents => Set<RawDocument>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RawDocument>().ToTable("ProviderReviewJsonDocuments")
                .Property(document => document.Document).HasColumnType("jsonb");
    }

    [Fact]
    public async Task Json_document_text_comparison_executes_and_returns_the_matching_row()
    {
        await fixture.CreateTablesOnceAsync<Context>(o => new(o));
        const string document = "{\"name\":\"x\"}";

        await using (var context = fixture.CreateContext<Context>(o => new(o)))
        {
            context.Documents.Add(new RawDocument { Document = document });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = fixture.CreateContext<Context>(o => new(o)))
        {
            var count = await context.Documents.CountAsync(
                row => row.Document == document,
                TestContext.Current.CancellationToken);
            Assert.Equal(1, count);
        }

        await fixture.ExecuteAsync("DELETE FROM \"ProviderReviewJsonDocuments\"");
    }
}

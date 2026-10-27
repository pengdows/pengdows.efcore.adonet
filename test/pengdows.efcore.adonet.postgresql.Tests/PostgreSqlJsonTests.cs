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

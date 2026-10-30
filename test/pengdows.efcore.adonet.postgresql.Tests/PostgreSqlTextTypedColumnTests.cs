using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     A string over a json, jsonb or xml column is a text value PostgreSQL types by the column: the parameter names that type
///     (as Npgsql needs, DataTypeName), so the server takes it. Without it PostgreSQL rejects the text ("column is of type json
///     but expression is of type text").
/// </summary>
public class PostgreSqlTextTypedColumnTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private readonly string _connectionString = fixture.ConnectionStringFor("texttyped_" + Guid.NewGuid().ToString("N")[..12]);

    private DocumentContext Context()
    {
        var context = fixture.CreateContext<DocumentContext>(o => new DocumentContext(o));
        context.Database.SetConnectionString(_connectionString);
        return context;
    }

    public async ValueTask InitializeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureCreatedAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureDeletedAsync(Ct);
    }

    [Fact]
    public async Task Json_jsonb_and_xml_strings_round_trip_and_match_by_parameter()
    {
        const string json = "{\"a\": 1}";
        const string jsonb = "{\"b\": [true, null]}";
        const string xml = "<v>1</v>";
        await using (var context = Context())
        {
            context.Documents.Add(new Document { Id = 1, Json = json, Jsonb = jsonb, Xml = xml });
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = Context();
        var row = await verify.Documents.AsNoTracking().SingleAsync(d => d.Id == 1, Ct);
        Assert.Equal(xml, row.Xml);
        Assert.Equal(jsonb, row.Jsonb);
        Assert.Equal(json, row.Json);

        var matched = await verify.Documents.AsNoTracking().Where(d => d.Jsonb == jsonb).Select(d => d.Id).SingleAsync(Ct);
        Assert.Equal(1, matched);
    }

    public sealed class Document
    {
        public int Id { get; set; }
        public string? Json { get; set; }
        public string? Jsonb { get; set; }
        public string? Xml { get; set; }
    }

    public sealed class DocumentContext(DbContextOptions<DocumentContext> options) : DbContext(options)
    {
        public DbSet<Document> Documents => Set<Document>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Document>(e =>
            {
                e.Property(d => d.Json).HasColumnType("json");
                e.Property(d => d.Jsonb).HasColumnType("jsonb");
                e.Property(d => d.Xml).HasColumnType("xml");
            });
    }
}

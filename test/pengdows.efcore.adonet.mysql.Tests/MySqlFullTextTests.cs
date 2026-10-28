using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     MySQL full-text search, as Pomelo has it: an index marked IsFullText is a FULLTEXT index, and EF.Functions.Match (the
///     relevance) and IsMatch (whether it matches) are MATCH (column) AGAINST (pattern [IN ... MODE]) over it. Provider-only:
///     FULLTEXT is MySQL's. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlDbFunctionsExtensions.cs
/// </summary>
public abstract class MySqlFullTextTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : MySqlFixture
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private readonly string _connectionString = fixture.ConnectionStringFor("fulltext_" + Guid.NewGuid().ToString("N")[..12]);

    private ArticleContext Context()
    {
        var context = fixture.CreateContext<ArticleContext>(o => new ArticleContext(o));
        context.Database.SetConnectionString(_connectionString);
        return context;
    }

    public async ValueTask InitializeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureCreatedAsync(Ct);
        context.AddRange(
            new Article { Title = "Tuning databases for speed" },
            new Article { Title = "Cooking with cast iron" },
            new Article { Title = "Database indexes explained" });
        await context.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureDeletedAsync(Ct);
    }

    [Fact]
    public async Task The_index_is_fulltext_and_is_match_finds_the_matching_rows()
    {
        await using var context = Context();

        var natural = await context.Articles
            .Where(a => EF.Functions.IsMatch(a.Title, "database", MySqlMatchSearchMode.NaturalLanguage))
            .Select(a => a.Title).OrderBy(t => t).ToListAsync(Ct);
        var boolean = await context.Articles
            .Where(a => EF.Functions.IsMatch(a.Title, "+cast -database", MySqlMatchSearchMode.Boolean))
            .Select(a => a.Title).ToListAsync(Ct);

        Assert.Equal(["Database indexes explained"], natural);
        Assert.Equal(["Cooking with cast iron"], boolean);
    }

    [Fact]
    public async Task Match_is_the_relevance()
    {
        await using var context = Context();

        var relevance = await context.Articles
            .OrderBy(a => a.Id)
            .Select(a => EF.Functions.Match(a.Title, "cooking", MySqlMatchSearchMode.NaturalLanguage))
            .ToListAsync(Ct);

        Assert.Equal(0, relevance[0]);
        Assert.True(relevance[1] > 0);
    }

    public sealed class Article
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    public sealed class ArticleContext(DbContextOptions<ArticleContext> options) : DbContext(options)
    {
        public DbSet<Article> Articles => Set<Article>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Article>(b =>
            {
                b.Property(a => a.Title).HasMaxLength(200);
                b.HasIndex(a => a.Title).IsFullText();
            });
    }
}

public class MySqlConnectorFullTextTests(MySqlFixture fixture) : MySqlFullTextTests<MySqlFixture>(fixture);

public class MySqlDataFullTextTests(MySqlDataFixture fixture) : MySqlFullTextTests<MySqlDataFixture>(fixture);

public class MariaDbFullTextTests(MariaDbFixture fixture) : MySqlFullTextTests<MariaDbFixture>(fixture);

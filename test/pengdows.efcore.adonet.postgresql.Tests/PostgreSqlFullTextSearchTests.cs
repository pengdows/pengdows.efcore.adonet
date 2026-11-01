using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     PostgreSQL full-text search as Npgsql's provider has it: tsvector and tsquery columns and values, the functions that make
///     them, their operators and ranking, and a tsvector column the database generates. Provider-only: full-text search with
///     these types is PostgreSQL's (MySQL's MATCH is <c>MySqlFullTextExtensions</c>). Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlFullTextSearchMethodTranslator.cs
/// </summary>
public sealed class PostgreSqlFullTextSearchTests
{
    private sealed class Article
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Body { get; set; }
        public NpgsqlTsVector SearchVector { get; set; } = null!;
        public NpgsqlTsQuery? Saved { get; set; }
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Article> Articles => Set<Article>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Article>().HasGeneratedTsVectorColumn(a => a.SearchVector, "english", a => new { a.Title, a.Body });
    }

    private static Context Create()
        => new(new DbContextOptionsBuilder<Context>().UseAdoNetPostgreSql("Host=localhost").Options);

    [Fact]
    public void Text_search_types_are_tsvector_and_tsquery_columns()
    {
        using var context = Create();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"Saved\" tsquery", script);
    }

    [Fact]
    public void A_generated_tsvector_column_is_stored_and_reads_its_columns()
    {
        using var context = Create();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains(
            "\"SearchVector\" tsvector GENERATED ALWAYS AS (to_tsvector('english', \"Title\" || ' ' || coalesce(\"Body\", ''))) STORED",
            script);
    }

    [Fact]
    public void A_generated_tsvector_column_is_not_written()
    {
        using var context = Create();

        var property = context.Model.FindEntityType(typeof(Article))!.FindProperty(nameof(Article.SearchVector))!;

        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public void Matches_is_the_match_operator_over_a_query_function()
    {
        using var context = Create();
        var terms = "cat & dog";

        var plain = context.Articles.Where(a => a.SearchVector.Matches("fat cats")).ToQueryString();
        var query = context.Articles.Where(a => a.SearchVector.Matches(EF.Functions.ToTsQuery("english", terms))).ToQueryString();
        var web = context.Articles.Where(a => a.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("\"fat cat\" -dog"))).ToQueryString();
        var phrase = context.Articles.Where(a => a.SearchVector.Matches(EF.Functions.PhraseToTsQuery("fat cat"))).ToQueryString();

        Assert.Contains("a.\"SearchVector\" @@ plainto_tsquery('fat cats')", plain);
        Assert.Contains("a.\"SearchVector\" @@ to_tsquery('english', @terms)", query);
        Assert.Contains("websearch_to_tsquery('\"fat cat\" -dog')", web);
        Assert.Contains("phraseto_tsquery('fat cat')", phrase);
    }

    [Fact]
    public void A_configuration_parameter_is_cast_to_regconfig()
    {
        using var context = Create();
        var config = "simple";

        var sql = context.Articles.Where(a => EF.Functions.ToTsVector(config, a.Title).Matches("x")).ToQueryString();

        Assert.Contains("to_tsvector(CAST(@config AS regconfig), a.\"Title\")", sql);
    }

    [Fact]
    public void Query_operators_and_vector_functions()
    {
        using var context = Create();

        var sql = context.Articles
            .Select(a => new
            {
                Both = EF.Functions.PlainToTsQuery("a").And(EF.Functions.PlainToTsQuery("b")).ToNegative(),
                Either = EF.Functions.PlainToTsQuery("a").Or(EF.Functions.PlainToTsQuery("b")),
                Weighted = a.SearchVector.SetWeight(NpgsqlTsVector.Lexeme.Weight.A).Concat(a.SearchVector.SetWeight('b')),
                Length = a.SearchVector.ToStripped().GetLength(),
                Headline = EF.Functions.PlainToTsQuery("a").GetResultHeadline(a.Title, "MaxWords=10")
            })
            .ToQueryString();

        Assert.Contains("!!(plainto_tsquery('a') && plainto_tsquery('b'))", sql);
        Assert.Contains("plainto_tsquery('a') || plainto_tsquery('b')", sql);
        Assert.Contains("setweight(a.\"SearchVector\", 'A') || setweight(a.\"SearchVector\", 'b')", sql);
        Assert.Contains("length(strip(a.\"SearchVector\"))", sql);
        Assert.Contains("ts_headline(a.\"Title\", plainto_tsquery('a'), 'MaxWords=10')", sql);
    }

    [Fact]
    public void A_tsquery_parameter_binds_as_tsquery_and_a_literal_casts()
    {
        using var context = Create();
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(NpgsqlTsQuery))!;
        using var command = new NpgsqlCommand();

        var query = new NpgsqlTsQueryAnd(new NpgsqlTsQueryLexeme("it's"), new NpgsqlTsQueryLexeme("b"));

        var parameter = (NpgsqlParameter)mapping.CreateParameter(command, "p", query);

        Assert.Equal(NpgsqlDbType.TsQuery, parameter.NpgsqlDbType);
        Assert.Contains("'", query.ToString());
        Assert.Equal("'" + query.ToString().Replace("'", "''") + "'::tsquery", mapping.GenerateSqlLiteral(query));
    }

    [Fact]
    public void Unaccent_takes_an_optional_dictionary()
    {
        using var context = Create();

        var sql = context.Articles.Select(a => EF.Functions.Unaccent("unaccent", a.Title)).ToQueryString();

        Assert.Contains("unaccent('unaccent', a.\"Title\")", sql);
    }
}

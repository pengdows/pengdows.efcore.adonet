using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     Native PostgreSQL arrays for primitive collections, as Npgsql's provider maps them, and its translations over them.
///     Npgsql maps every primitive collection to an array by default; here a collection stays a jsonb document, as on the other
///     five providers, unless the model opts in (<c>UsePostgreSqlArrays</c>) or a column's type is an array type. Provider-only:
///     arrays are PostgreSQL's. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Internal/NpgsqlQueryableMethodTranslatingExpressionVisitor.cs
/// </summary>
public sealed class PostgreSqlArrayTests
{
    private sealed class Post
    {
        public int Id { get; set; }
        public int[] Scores { get; set; } = [];
        public List<string> Tags { get; set; } = [];
        public int?[] Optional { get; set; } = [];
    }

    private sealed class Note
    {
        public int Id { get; set; }
        public int[] Scores { get; set; } = [];
        public List<string> Tags { get; set; } = [];
    }

    private sealed class ArrayContext(DbContextOptions<ArrayContext> options) : DbContext(options)
    {
        public DbSet<Post> Posts => Set<Post>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UsePostgreSqlArrays();
            modelBuilder.Entity<Post>();
        }
    }

    private sealed class JsonContext(DbContextOptions<JsonContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Note>().Property(n => n.Tags).HasColumnType("text[]");
    }

    private static ArrayContext Arrays()
        => new(new DbContextOptionsBuilder<ArrayContext>().UseAdoNetPostgreSql("Host=localhost").Options);

    private static JsonContext Json()
        => new(new DbContextOptionsBuilder<JsonContext>().UseAdoNetPostgreSql("Host=localhost").Options);

    [Fact]
    public void Opted_in_collections_are_array_columns()
    {
        using var context = Arrays();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"Scores\" INTEGER[] NOT NULL", script);
        Assert.Contains("\"Tags\" VARCHAR[] NOT NULL", script);
        Assert.Contains("\"Optional\" INTEGER[] NOT NULL", script);
    }

    [Fact]
    public void Collections_stay_json_unless_opted_in_and_an_array_column_type_maps_one()
    {
        using var context = Json();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"Scores\" jsonb NOT NULL", script);
        Assert.Contains("\"Tags\" text[] NOT NULL", script);
    }

    [Fact]
    public void Contains_is_equal_to_any()
    {
        using var context = Arrays();

        var sql = context.Posts.Where(p => p.Scores.Contains(3)).ToQueryString();

        Assert.Contains("3 = ANY (p.\"Scores\")", sql);
    }

    [Fact]
    public void Contains_of_a_column_value_is_equal_to_any()
    {
        using var context = Arrays();

        var sql = context.Posts.Where(p => p.Tags.Contains("x" + p.Id)).Select(p => p.Id).ToQueryString();

        Assert.Contains("= ANY (p.\"Tags\")", sql);
        Assert.DoesNotContain("unnest", sql);
    }

    [Fact]
    public void Length_and_count_are_cardinality()
    {
        using var context = Arrays();

        var length = context.Posts.Where(p => p.Scores.Length == 2).ToQueryString();
        var count = context.Posts.Where(p => p.Tags.Count == 2).ToQueryString();

        Assert.Contains("cardinality(p.\"Scores\") = 2", length);
        Assert.Contains("cardinality(p.\"Tags\") = 2", count);
    }

    [Fact]
    public void Indexing_is_one_based_subscripting()
    {
        using var context = Arrays();

        var array = context.Posts.Where(p => p.Scores[0] == 7).ToQueryString();
        var list = context.Posts.Where(p => p.Tags[1] == "b").ToQueryString();

        Assert.Contains("p.\"Scores\"[1] = 7", array);
        Assert.Contains("p.\"Tags\"[2] = 'b'", list);
    }

    [Fact]
    public void Indexing_by_a_column_adds_one()
    {
        using var context = Arrays();

        var sql = context.Posts.Where(p => p.Scores[p.Id] == 7).ToQueryString();

        Assert.Contains("p.\"Scores\"[p.\"Id\" + 1] = 7", sql);
    }

    [Fact]
    public void Any_without_a_predicate_is_a_cardinality_test()
    {
        using var context = Arrays();

        var sql = context.Posts.Where(p => p.Tags.Any()).ToQueryString();

        Assert.Contains("cardinality(p.\"Tags\") > 0", sql);
    }

    [Fact]
    public void Any_with_a_predicate_queries_the_unnested_elements_in_order()
    {
        using var context = Arrays();

        var sql = context.Posts.Where(p => p.Scores.Any(s => s > 10)).ToQueryString();

        Assert.Contains("FROM unnest(p.\"Scores\") WITH ORDINALITY AS s(value, ordinality)", sql);
        Assert.Contains("WHERE s.value > 10", sql);
    }

    [Fact]
    public void Projecting_a_filtered_collection_reads_the_unnested_elements()
    {
        using var context = Arrays();

        var sql = context.Posts.Select(p => p.Scores.Where(s => s > 1).Sum()).ToQueryString();

        Assert.Contains("unnest(p.\"Scores\") WITH ORDINALITY", sql);
    }

    [Fact]
    public void Concatenation_is_the_array_operator()
    {
        using var context = Arrays();

        var sql = context.Posts.Where(p => p.Scores.Concat(p.Scores).Count() == 4).ToQueryString();

        Assert.Contains("cardinality(p.\"Scores\" || p.\"Scores\") = 4", sql);
    }

    [Fact]
    public void Array_operators_from_ef_functions()
    {
        using var context = Arrays();
        int[] wanted = [1, 2];

        var overlaps = context.Posts.Where(p => EF.Functions.ArrayOverlaps(p.Scores, wanted)).ToQueryString();
        var contains = context.Posts.Where(p => EF.Functions.ArrayContains(p.Scores, wanted)).ToQueryString();
        var containedBy = context.Posts.Where(p => EF.Functions.ArrayContainedBy(p.Scores, wanted)).ToQueryString();

        Assert.Contains("p.\"Scores\" && @wanted", overlaps);
        Assert.Contains("p.\"Scores\" @> @wanted", contains);
        Assert.Contains("p.\"Scores\" <@ @wanted", containedBy);
    }

    [Fact]
    public void Any_contains_of_another_array_is_overlap_and_all_is_containment()
    {
        using var context = Arrays();

        var any = context.Posts.Where(p => p.Scores.Any(s => p.Scores.Contains(s))).ToQueryString();
        var all = context.Posts.Where(p => p.Tags.All(t => p.Tags.Contains(t))).ToQueryString();

        Assert.Contains("WHERE p.\"Scores\" && p.\"Scores\"", any);
        Assert.Contains("WHERE p.\"Tags\" <@ p.\"Tags\"", all);
    }

    [Fact]
    public void Any_over_an_element_expression_stays_a_subquery()
    {
        using var context = Arrays();

        var sql = context.Posts.Where(p => p.Scores.Any(s => p.Scores.Contains(s + 1))).ToQueryString();

        Assert.Contains("EXISTS", sql);
        Assert.Contains("(s.value + 1) = ANY (p.\"Scores\")", sql);
    }

    [Fact]
    public void Contains_of_null_finds_a_null_element()
    {
        using var context = Arrays();

        var sql = context.Posts.Where(p => p.Optional.Contains(null)).ToQueryString();

        Assert.Contains("array_position(p.\"Optional\", NULL) IS NOT NULL", sql);
    }

    [Fact]
    public void Ordering_an_array_keeps_its_subquery()
    {
        using var context = Arrays();

        var sql = context.Posts.Select(p => p.Scores.OrderBy(s => s).First()).ToQueryString();

        Assert.Contains("ORDER BY s.value", sql);
        Assert.DoesNotContain("[1]", sql);
    }

    [Fact]
    public void An_array_parameter_binds_as_the_array_type()
    {
        using var context = Arrays();
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(int[]), "integer[]")!;
        using var command = new NpgsqlCommand();

        var parameter = mapping.CreateParameter(command, "p", new[] { 1, 2 });

        Assert.Equal("integer[]", mapping.StoreType);
        Assert.Equal(NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer, ((NpgsqlParameter)parameter).NpgsqlDbType);
    }

    [Fact]
    public void A_client_composed_enumerable_binds_as_a_list()
    {
        using var context = Arrays();
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(int[]), "integer[]")!;
        using var command = new NpgsqlCommand();

        var parameter = mapping.CreateParameter(command, "p", new[] { 1, 2, 3 }.Where(i => i > 1));

        Assert.Equal([2, 3], Assert.IsType<List<int>>(parameter.Value));
    }

    [Fact]
    public void An_array_literal_is_an_array_constructor()
    {
        using var context = Arrays();
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(string[]), "text[]")!;

        Assert.Equal("ARRAY['a','it''s']::text[]", mapping.GenerateSqlLiteral(new[] { "a", "it's" }));
        Assert.Equal("ARRAY[]::text[]", mapping.GenerateSqlLiteral(Array.Empty<string>()));
    }

    [Fact]
    public void Changing_an_element_in_place_is_a_change()
    {
        using var context = Arrays();
        var post = new Post { Id = 1, Scores = [1, 2], Tags = ["a"] };
        context.Attach(post);

        post.Scores[0] = 5;
        post.Tags.Add("b");
        context.ChangeTracker.DetectChanges();

        Assert.True(context.Entry(post).Property(p => p.Scores).IsModified);
        Assert.True(context.Entry(post).Property(p => p.Tags).IsModified);
    }
}

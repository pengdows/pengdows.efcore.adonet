using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     MySQL's JSON functions through <c>EF.Functions</c>, as Pomelo's MySqlJsonDbFunctionsExtensions has them: AsJson,
///     JsonType, JsonQuote, JsonUnquote, JsonExtract, JsonOverlaps, JsonContains, JsonContainsPath(Any/All) and JsonSearchAny.
///     SQL shape only, on MySQL and MariaDB: MariaDB has the same functions, but no JSON type to CAST text to. Provider-only:
///     these are MySQL's JSON functions. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionTranslators/Internal/MySqlJsonDbFunctionsTranslator.cs
/// </summary>
public class MySqlJsonFunctionsTests
{
    public sealed class Customer
    {
        public int Id { get; set; }
        public string Doc { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public sealed class Address
    {
        public string City { get; set; } = "";
    }

    private sealed class CustomerContext(DbContextOptions<CustomerContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>().Property(c => c.Doc).HasColumnType("json");
    }

    private static CustomerContext Context(bool mariaDb = false)
        => new(new DbContextOptionsBuilder<CustomerContext>()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance))
            .Options);

    [Fact]
    public void As_json_casts_text_to_json_on_MySql()
    {
        using var context = Context();

        var sql = context.Customers.Where(c => EF.Functions.JsonContains(c.Doc, EF.Functions.AsJson("{\"a\": 1}"))).ToQueryString();

        Assert.Contains("JSON_CONTAINS(`c`.`Doc`, CAST('{\"a\": 1}' AS JSON))", sql);
    }

    // MariaDB's JSON is LONGTEXT, with no JSON type to CAST to; a JSON function's result is taken as JSON.
    [Fact]
    public void As_json_makes_text_json_through_a_json_function_on_MariaDb()
    {
        using var context = Context(mariaDb: true);

        var sql = context.Customers.Where(c => EF.Functions.JsonContains(c.Doc, EF.Functions.AsJson("{\"a\": 1}"))).ToQueryString();

        Assert.Contains("JSON_CONTAINS(`c`.`Doc`, JSON_COMPACT('{\"a\": 1}'))", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_type_quote_and_unquote(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var sql = context.Customers
            .Select(c => new { Type = EF.Functions.JsonType(c.Doc), Quoted = EF.Functions.JsonQuote(c.Name), Unquoted = EF.Functions.JsonUnquote(c.Doc) })
            .ToQueryString();

        Assert.Contains("JSON_TYPE(`c`.`Doc`)", sql);
        Assert.Contains("JSON_QUOTE(`c`.`Name`)", sql);
        Assert.Contains("JSON_UNQUOTE(`c`.`Doc`)", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_contains_with_and_without_a_path(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var whole = context.Customers.Where(c => EF.Functions.JsonContains(c.Doc, "1")).ToQueryString();
        var atPath = context.Customers.Where(c => EF.Functions.JsonContains(c.Doc, "1", "$.a")).ToQueryString();

        Assert.Contains("JSON_CONTAINS(`c`.`Doc`, '1')", whole);
        Assert.Contains("JSON_CONTAINS(`c`.`Doc`, '1', '$.a')", atPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_overlaps(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var sql = context.Customers.Where(c => EF.Functions.JsonOverlaps(c.Doc, "[1, 2]")).ToQueryString();

        Assert.Contains("JSON_OVERLAPS(`c`.`Doc`, '[1, 2]')", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_contains_path_one_any_and_all(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var one = context.Customers.Where(c => EF.Functions.JsonContainsPath(c.Doc, "$.a")).ToQueryString();
        var any = context.Customers.Where(c => EF.Functions.JsonContainsPathAny(c.Doc, "$.a", "$.b")).ToQueryString();
        var all = context.Customers.Where(c => EF.Functions.JsonContainsPathAll(c.Doc, "$.a", "$.b")).ToQueryString();

        Assert.Contains("JSON_CONTAINS_PATH(`c`.`Doc`, 'one', '$.a')", one);
        Assert.Contains("JSON_CONTAINS_PATH(`c`.`Doc`, 'one', '$.a', '$.b')", any);
        Assert.Contains("JSON_CONTAINS_PATH(`c`.`Doc`, 'all', '$.a', '$.b')", all);
    }

    // JSON_SEARCH is null where nothing matches, whatever its arguments; a NULL escape is the default, a backslash.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_search_any_with_a_path_and_an_escape(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var anywhere = context.Customers.Where(c => EF.Functions.JsonSearchAny(c.Doc, "Bob%")).ToQueryString();
        var atPath = context.Customers.Where(c => EF.Functions.JsonSearchAny(c.Doc, "Bob%", "$.names")).ToQueryString();
        var escaped = context.Customers.Where(c => EF.Functions.JsonSearchAny(c.Doc, "Bob|%", "$.names", "|")).ToQueryString();

        Assert.Contains("JSON_SEARCH(`c`.`Doc`, 'one', 'Bob%') IS NOT NULL", anywhere);
        Assert.Contains("JSON_SEARCH(`c`.`Doc`, 'one', 'Bob%', NULL, '$.names') IS NOT NULL", atPath);
        Assert.Contains("JSON_SEARCH(`c`.`Doc`, 'one', 'Bob|%', '|', '$.names') IS NOT NULL", escaped);
    }

    // A string is the JSON text JSON_EXTRACT returns, as in Pomelo; several paths return an array of their values.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_extract_of_text_is_the_json_text(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var one = context.Customers.Select(c => EF.Functions.JsonExtract<string>(c.Doc, "$.a")).ToQueryString();
        var several = context.Customers.Select(c => EF.Functions.JsonExtract<string>(c.Doc, "$.a", "$.b")).ToQueryString();

        Assert.Contains("JSON_EXTRACT(`c`.`Doc`, '$.a')", one);
        Assert.Contains("JSON_EXTRACT(`c`.`Doc`, '$.a', '$.b')", several);
    }

    // A scalar is read as its own type, as JSON_VALUE would: a comparison with it compares values, not a JSON document
    // with text.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_extract_of_a_scalar_is_read_as_its_type(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var sql = context.Customers.Where(c => EF.Functions.JsonExtract<int>(c.Doc, "$.a") > 3).ToQueryString();

        Assert.Contains("CAST(JSON_UNQUOTE(JSON_EXTRACT(`c`.`Doc`, '$.a')) AS SIGNED)", sql);
        Assert.Contains("> 3", sql);
    }

    // A scalar from several paths would be an array: refused rather than failing when read.
    [Fact]
    public void Json_extract_of_a_scalar_from_several_paths_is_refused()
    {
        using var context = Context();

        Assert.Throws<InvalidOperationException>(
            () => context.Customers.Where(c => EF.Functions.JsonExtract<int>(c.Doc, "$.a", "$.b") > 0).ToQueryString());
    }

    // An object or collection is the JSON text, deserialized when read.
    [Fact]
    public void Json_extract_of_an_object_or_a_collection_is_the_json_text()
    {
        using var context = Context();

        var address = context.Customers.Select(c => EF.Functions.JsonExtract<Address>(c.Doc, "$.address")).ToQueryString();
        var numbers = context.Customers.Select(c => EF.Functions.JsonExtract<int[]>(c.Doc, "$.a", "$.b")).ToQueryString();

        Assert.Contains("JSON_EXTRACT(`c`.`Doc`, '$.address')", address);
        Assert.Contains("JSON_EXTRACT(`c`.`Doc`, '$.a', '$.b')", numbers);
    }

    // Read through fakeDb, which returns what a server would: the JSON text of the object, and the text of the scalar.
    [Fact]
    public async Task Json_extract_reads_an_object_and_a_scalar_back()
    {
        await using var connection = new pengdows.crud.fakeDb.fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=MySql" };
        await using var context = new CustomerContext(new DbContextOptionsBuilder<CustomerContext>()
            .UseAdoNetMySql(connection, contextOwnsConnection: false)
            .Options);
        connection.EnqueueReaderResult([new Dictionary<string, object?> { ["c0"] = "{\"City\": \"Oslo\"}" }]);
        connection.EnqueueReaderResult([new Dictionary<string, object?> { ["c0"] = 42 }]);

        var address = await context.Customers.Select(c => EF.Functions.JsonExtract<Address>(c.Doc, "$.address"))
            .SingleAsync(TestContext.Current.CancellationToken);
        var number = await context.Customers.Select(c => EF.Functions.JsonExtract<int>(c.Doc, "$.a"))
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Oslo", address.City);
        Assert.Equal(42, number);
    }

    [Fact]
    public void Json_functions_throw_outside_a_query()
    {
        Assert.Throws<InvalidOperationException>(() => EF.Functions.JsonType("{}"));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.JsonContains("{}", "1"));
        Assert.Equal("{}", EF.Functions.AsJson("{}"));
    }
}

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pengdows.efcore.adonet.mysql.Json.Newtonsoft;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Pomelo's Newtonsoft.Json package: a POCO on a json column (serialized, its members and their JsonProperty names read in
///     queries as JSON paths, a list's Count as JSON_LENGTH), JObject and JArray on json columns (the index by name or position,
///     Value&lt;T&gt;(), Count), and the change tracking of what is inside them by MySqlCommonJsonChangeTrackingOptions. SQL shape
///     on MySQL and MariaDB, and fakeDb. Provider-only. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/tree/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.Json.Newtonsoft
/// </summary>
public class MySqlNewtonsoftJsonTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class Address
    {
        public string City { get; set; } = "";

        [JsonProperty("zip")]
        public string Zip { get; set; } = "";

        public List<string> Lines { get; set; } = [];
    }

    public sealed class Customer
    {
        public int Id { get; set; }
        public Address Address { get; set; } = new();
        public JObject Profile { get; set; } = new();
        public JArray Tags { get; set; } = new();
    }

    public class CustomerContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>().Property(c => c.Address).HasColumnType("json");
    }

    // The address alone tracks the changes inside it.
    public sealed class TrackedCustomerContext(DbContextOptions options) : CustomerContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>().Property(c => c.Address).HasColumnType("json")
                .UseJsonChangeTrackingOptions(MySqlCommonJsonChangeTrackingOptions.FullHierarchyOptimizedFast);
    }

    private static CustomerContext Context(bool mariaDb = false)
        => new(new DbContextOptionsBuilder<CustomerContext>()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance).UseNewtonsoftJson())
            .Isolated("newtonsoft-json " + mariaDb)
            .Options);

    [Fact]
    public void A_poco_and_json_tokens_are_json_columns()
    {
        using var context = Context();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("`Address` json NOT NULL", script);
        Assert.Contains("`Profile` json NOT NULL", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`Tags` json NOT NULL", script, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_pocos_members_are_json_paths(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var city = context.Customers.Where(c => c.Address.City == "Oslo").ToQueryString();
        var zip = context.Customers.Where(c => c.Address.Zip == "0150").ToQueryString();
        var count = context.Customers.Where(c => c.Address.Lines.Count > 1).ToQueryString();

        Assert.Contains("JSON_UNQUOTE(JSON_EXTRACT(`c`.`Address`, '$.City'))", city);
        Assert.Contains("'$.zip'", zip);
        Assert.Contains("JSON_LENGTH(", count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_tokens_index_value_and_count_are_json_paths(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var name = context.Customers.Where(c => c.Profile["name"]!.Value<string>() == "Bob").ToQueryString();
        var age = context.Customers.Where(c => c.Profile["age"]!.Value<int>() > 30).ToQueryString();
        var first = context.Customers.Where(c => c.Tags[0]!.Value<string>() == "x").ToQueryString();
        var count = context.Customers.Where(c => c.Tags.Count > 2).ToQueryString();

        Assert.Contains("JSON_UNQUOTE(JSON_EXTRACT(`c`.`Profile`, '$.name'))", name);
        Assert.Contains("AS SIGNED)", age);
        Assert.Contains("'$[0]'", first);
        Assert.Contains("JSON_LENGTH(`c`.`Tags`)", count);
    }

    [Fact]
    public async Task A_poco_and_tokens_round_trip_through_fakeDb()
    {
        var db = new FakeMySql();
        await using (var context = db.Context<CustomerContext>(o => new CustomerContext(o), adoNet: o => o.UseNewtonsoftJson()))
        {
            db.Returns([("Id", 1)]);
            context.Customers.Add(new Customer
            {
                Address = new Address { City = "Oslo", Zip = "0150" },
                Profile = JObject.Parse("{\"name\":\"Bob\"}"),
                Tags = new JArray("a", "b")
            });
            await context.SaveChangesAsync(Ct);
        }

        db.Returns([("Id", 1), ("Address", "{\"City\": \"Bergen\", \"zip\": \"5003\"}"), ("Profile", "{\"name\": \"Eve\"}"), ("Tags", "[\"x\"]")]);
        await using var reading = db.Context<CustomerContext>(o => new CustomerContext(o), adoNet: o => o.UseNewtonsoftJson());
        var customer = await reading.Customers.SingleAsync(Ct);

        Assert.Contains("\"City\":\"Oslo\"", db.Parameters[0]);
        Assert.Contains("\"zip\":\"0150\"", db.Parameters[0]);
        Assert.Contains("[\"a\",\"b\"]", db.Parameters[0]);
        Assert.Equal(("Bergen", "5003"), (customer.Address.City, customer.Address.Zip));
        Assert.Equal("Eve", customer.Profile["name"]!.Value<string>());
        Assert.Equal("x", customer.Tags[0]!.Value<string>());
    }

    [Theory]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly, false, false)]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.FullHierarchyOptimizedFast, false, true)]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.FullHierarchyOptimizedSemantically, false, true)]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.FullHierarchySemantically, false, true)]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly, true, true)]
    public void A_change_inside_a_poco_or_a_token_is_tracked_as_the_options_say(
        MySqlCommonJsonChangeTrackingOptions everyColumn,
        bool perProperty,
        bool tracked)
    {
        var options = new DbContextOptionsBuilder()
            .UseAdoNetMySql("Server=localhost", o => o.UseNewtonsoftJson(everyColumn))
            .Isolated("newtonsoft-json " + everyColumn)
            .Options;
        using var context = perProperty ? new TrackedCustomerContext(options) : new CustomerContext(options);
        var customer = new Customer { Id = 1, Address = new Address { City = "Oslo" }, Profile = JObject.Parse("{\"a\":1}") };
        context.Attach(customer);

        customer.Address.City = "Bergen";
        customer.Profile["a"] = 2;
        context.ChangeTracker.DetectChanges();

        Assert.Equal(tracked, context.Entry(customer).Property(c => c.Address).IsModified);
        Assert.Equal(tracked && !perProperty, context.Entry(customer).Property(c => c.Profile).IsModified);
    }
}

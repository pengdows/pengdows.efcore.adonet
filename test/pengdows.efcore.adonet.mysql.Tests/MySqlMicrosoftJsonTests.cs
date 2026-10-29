using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.mysql.Json.Microsoft;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Pomelo's System.Text.Json package: a POCO on a json column (serialized, its members and their JsonPropertyName read in
///     queries as JSON paths, a list's Count as JSON_LENGTH), JsonDocument and JsonElement on json columns (GetProperty, the
///     index, Get* and GetArrayLength in queries), and the change tracking of what is inside them by
///     MySqlCommonJsonChangeTrackingOptions. SQL shape on MySQL and MariaDB, and fakeDb. Provider-only. Behaviour reference (not
///     copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/tree/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.Json.Microsoft
/// </summary>
public class MySqlMicrosoftJsonTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class Location
    {
        public double Lat { get; set; }
    }

    public sealed class Address
    {
        public string City { get; set; } = "";

        [JsonPropertyName("zip")]
        public string Zip { get; set; } = "";

        public Location Location { get; set; } = new();
        public List<string> Lines { get; set; } = [];
    }

    public sealed class Customer
    {
        public int Id { get; set; }
        public Address Address { get; set; } = new();
        public JsonDocument Profile { get; set; } = JsonDocument.Parse("{}");
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
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance).UseMicrosoftJson())
            .Isolated("microsoft-json " + mariaDb)
            .Options);

    [Fact]
    public void A_poco_and_a_json_document_are_json_columns()
    {
        using var context = Context();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("`Address` json NOT NULL", script);
        Assert.Contains("`Profile` json NOT NULL", script, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_pocos_members_are_json_paths(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var city = context.Customers.Where(c => c.Address.City == "Oslo").ToQueryString();
        var zip = context.Customers.Where(c => c.Address.Zip == "0150").ToQueryString();
        var lat = context.Customers.Where(c => c.Address.Location.Lat > 1.5).ToQueryString();

        Assert.Contains("JSON_UNQUOTE(JSON_EXTRACT(`c`.`Address`, '$.City'))", city);
        Assert.Contains("'$.zip'", zip);
        Assert.Contains("'$.Location.Lat'", lat);
        Assert.Contains("AS DOUBLE)", lat);
    }

    [Fact]
    public void A_lists_count_is_its_json_length_and_its_index_a_path()
    {
        using var context = Context();

        var count = context.Customers.Where(c => c.Address.Lines.Count > 1).ToQueryString();
        var any = context.Customers.Where(c => c.Address.Lines.Any()).ToQueryString();
        var first = context.Customers.Where(c => c.Address.Lines[0] == "a").ToQueryString();

        Assert.Contains("JSON_LENGTH(", count);
        Assert.Contains("'$.Lines'", count);
        Assert.Contains("JSON_LENGTH(", any);
        Assert.Contains("'$.Lines[0]'", first);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_json_documents_elements_are_json_paths(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var name = context.Customers.Where(c => c.Profile.RootElement.GetProperty("name").GetString() == "Bob").ToQueryString();
        var age = context.Customers.Where(c => c.Profile.RootElement.GetProperty("age").GetInt32() > 30).ToQueryString();
        var tags = context.Customers.Where(c => c.Profile.RootElement.GetProperty("tags").GetArrayLength() > 0).ToQueryString();
        var tag = context.Customers.Where(c => c.Profile.RootElement.GetProperty("tags")[0].GetString() == "x").ToQueryString();

        Assert.Contains("JSON_UNQUOTE(JSON_EXTRACT(`c`.`Profile`, '$.name'))", name);
        Assert.Contains("AS SIGNED)", age);
        Assert.Contains("JSON_LENGTH(", tags);
        Assert.Contains("'$.tags[0]'", tag);
    }

    // MySQL's JSON functions take the POCO's column, and a value read inside it.
    [Fact]
    public void Json_functions_take_a_poco_column()
    {
        using var context = Context();

        var path = context.Customers.Where(c => EF.Functions.JsonContainsPath(c.Address, "$.City")).ToQueryString();
        var lines = context.Customers.Where(c => EF.Functions.JsonContains(c.Address.Lines, "\"a\"")).ToQueryString();

        Assert.Contains("JSON_CONTAINS_PATH(`c`.`Address`, 'one', '$.City')", path);
        Assert.Contains("JSON_CONTAINS(JSON_EXTRACT(`c`.`Address`, '$.Lines'), '\"a\"')", lines);
    }

    // The POCO is sent as its JSON text and read back from it, as is the document.
    [Fact]
    public async Task A_poco_and_a_document_round_trip_through_fakeDb()
    {
        var db = new FakeMySql();
        await using (var context = db.Context<CustomerContext>(o => new CustomerContext(o), adoNet: o => o.UseMicrosoftJson()))
        {
            db.Returns([("Id", 1)]);
            context.Customers.Add(new Customer { Address = new Address { City = "Oslo", Zip = "0150" }, Profile = JsonDocument.Parse("{\"name\":\"Bob\"}") });
            await context.SaveChangesAsync(Ct);
        }

        db.Returns([("Id", 1), ("Address", "{\"City\": \"Bergen\", \"zip\": \"5003\", \"Lines\": [\"a\"]}"), ("Profile", "{\"name\": \"Eve\"}")]);
        await using var reading = db.Context<CustomerContext>(o => new CustomerContext(o), adoNet: o => o.UseMicrosoftJson());
        var customer = await reading.Customers.SingleAsync(Ct);

        Assert.Contains("\"City\":\"Oslo\"", db.Parameters[0]);
        Assert.Contains("\"zip\":\"0150\"", db.Parameters[0]);
        Assert.Contains("{\"name\":\"Bob\"}", db.Parameters[0]);
        Assert.Equal(("Bergen", "5003"), (customer.Address.City, customer.Address.Zip));
        Assert.Equal(["a"], customer.Address.Lines);
        Assert.Equal("Eve", customer.Profile.RootElement.GetProperty("name").GetString());
    }

    // RootPropertyOnly (the default) sees only a new value; the full-hierarchy options see a change inside it too, whether set
    // for every json column or for one property.
    [Theory]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly, false, false)]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.FullHierarchyOptimizedFast, false, true)]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.FullHierarchyOptimizedSemantically, false, true)]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.FullHierarchySemantically, false, true)]
    [InlineData(MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly, true, true)]
    public void A_change_inside_a_poco_is_tracked_as_the_options_say(
        MySqlCommonJsonChangeTrackingOptions everyColumn,
        bool perProperty,
        bool tracked)
    {
        var options = new DbContextOptionsBuilder()
            .UseAdoNetMySql("Server=localhost", o => o.UseMicrosoftJson(everyColumn))
            .Isolated("microsoft-json " + everyColumn)
            .Options;
        using var context = perProperty ? new TrackedCustomerContext(options) : new CustomerContext(options);
        var customer = new Customer { Id = 1, Address = new Address { City = "Oslo" } };
        context.Attach(customer);

        customer.Address.City = "Bergen";
        context.ChangeTracker.DetectChanges();

        Assert.Equal(tracked, context.Entry(customer).Property(c => c.Address).IsModified);
    }
}

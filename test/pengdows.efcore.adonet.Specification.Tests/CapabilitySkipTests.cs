using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.sqlite;
using pengdows.efcore.adonet.postgresql;


namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     A suite test that needs what the dialect says the database hasn't got is skipped, naming the database: a query needing
///     a lateral join (<see cref="ISqlDialect.LateralJoins" /> null), through either way the suite builds a context, and stored
///     procedures (<see cref="ISqlDialect.SupportsStoredProcedures" /> false); where the database has them it runs, and a host
///     that hasn't written its procedures fails. Runs on: SQLite (skip), PostgreSQL (runs or fails). No database is contacted.
/// </summary>
public sealed class CapabilitySkipTests
{
    public class Item
    {
        public int Id { get; set; }
    }

    public class Part
    {
        public int Id { get; set; }
        public string? Label { get; set; }
    }

    private sealed class ItemContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
        public DbSet<Part> Parts => Set<Part>();
    }

    // A paged subquery correlated by more than equality, joined per row: EF's CROSS APPLY.
    private static string FirstPartPerItem(DbContextOptions options)
    {
        using var context = new ItemContext(options);
        return context.Items
            .SelectMany(i => context.Parts.Where(p => p.Id > i.Id).OrderBy(p => p.Id).Take(1), (i, p) => new { i.Id, p.Label })
            .ToQueryString();
    }

    [Fact]
    public void Options_built_by_the_suite_skip_on_SQLite()
    {
        var host = new SqliteHost();
        var options = new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNetSqlite("Data Source=:memory:", o => TestServices.Use(host, o))
            .Options;

        var skip = Assert.Throws<CapabilitySkipException>(() => FirstPartPerItem(options));

        Assert.Contains("[SKIP:SQLite]", skip.Message);
        Assert.Contains("lateral join", skip.Message);
    }

    [Fact]
    public void A_service_provider_built_by_the_suite_skips_on_SQLite()
    {
        var host = new SqliteHost();
        var services = TestServices.Add(host, new ServiceCollection().AddEntityFrameworkAdoNet(host.Dialect)).BuildServiceProvider();
        var options = new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNetSqlite("Data Source=:memory:")
            .UseInternalServiceProvider(services)
            .Options;

        var skip = Assert.Throws<CapabilitySkipException>(() => FirstPartPerItem(options));

        Assert.Contains("[SKIP:SQLite]", skip.Message);
    }

    [Fact]
    public void PostgreSQL_translates_the_lateral_join()
    {
        var host = new PostgreSqlHost();
        var options = new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNetPostgreSql("Host=localhost", o => TestServices.Use(host, o))
            .Options;

        Assert.Contains("CROSS JOIN LATERAL", FirstPartPerItem(options));
    }

    public class Widget
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class ProcedureContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Widget> Widgets => Set<Widget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Widget>().InsertUsingStoredProcedure(
                "Widget_Insert", p => p.HasParameter(w => w.Name).HasResultColumn(w => w.Id));
    }

    [Fact]
    public void A_model_mapped_to_procedures_skips_on_SQLite()
    {
        var host = new SqliteHost();
        using var context = new ProcedureContext(new DbContextOptionsBuilder<ProcedureContext>()
            .UseAdoNetSqlite("Data Source=:memory:", o => TestServices.Use(host, o))
            .Options);

        var skip = Assert.Throws<CapabilitySkipException>(() => context.Model);

        Assert.Contains("[SKIP:SQLite]", skip.Message);
        Assert.Contains(nameof(Widget), skip.Message);
    }

    [Fact]
    public void A_suite_needing_procedures_skips_where_the_database_has_none()
        => Assert.StartsWith(
            "[SKIP:SQLite]", Assert.IsType<CapabilitySkipException>(CapabilitySkips.NoProcedures<SqliteHost>("stored procedures")).Message);

    [Fact]
    public void A_suite_needing_procedures_fails_where_the_host_has_not_written_them()
        => Assert.IsType<NotSupportedException>(CapabilitySkips.NoProcedures<PostgreSqlHost>("stored procedures"));
}

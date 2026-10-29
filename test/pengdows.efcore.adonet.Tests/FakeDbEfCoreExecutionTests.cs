using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     EF Core materialization behaviors exercised against scripted fakeDb rows. These tests complement the provider
///     conformance suites by checking what EF does after a relational command has returned.
/// </summary>
public sealed class FakeDbEfCoreExecutionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tracking_query_reuses_one_entity_instance_for_duplicate_primary_keys()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<SimpleContext>(AdoNetDialect.Ansi, options => new SimpleContext(options));
        database.Returns(ItemRow(7, "same"), ItemRow(7, "same"));

        var items = await context.Items.OrderBy(item => item.Id).ToListAsync(Ct);

        Assert.Equal(2, items.Count);
        Assert.Same(items[0], items[1]);
        Assert.Same(items[0], context.ChangeTracker.Entries<SimpleItem>().Single().Entity);
    }

    [Fact]
    public async Task No_tracking_identity_resolution_reuses_rows_without_tracking_the_entity()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<SimpleContext>(AdoNetDialect.Ansi, options => new SimpleContext(options));
        database.Returns(ItemRow(8, "same"), ItemRow(8, "same"));

        var items = await context.Items.AsNoTrackingWithIdentityResolution().ToListAsync(Ct);

        Assert.Same(items[0], items[1]);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static Dictionary<string, object?> ItemRow(int id, string name)
        => new()
        {
            ["Id"] = id,
            ["Active"] = true,
            ["Name"] = name
        };

    private sealed class SimpleItem
    {
        public int Id { get; set; }
        public bool Active { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class SimpleContext(DbContextOptions<SimpleContext> options) : DbContext(options)
    {
        public DbSet<SimpleItem> Items => Set<SimpleItem>();
    }
}

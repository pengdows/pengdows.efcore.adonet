using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Set operations and aggregate shapers executed against scripted fakeDb scalar rows.</summary>
public sealed class FakeDbEfCoreSetAndAggregateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Grouping_and_aggregate_queries_materialize_the_provider_scalar()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<AggregateContext>(AdoNetDialect.Ansi, options => new AggregateContext(options));
        database.Returns(FakeDatabase.Row("Value", 2));

        var count = await context.Items.GroupBy(item => item.Active).Select(group => group.Count()).SingleAsync(Ct);

        Assert.Equal(2, count);
        Assert.Contains("COUNT", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Union_queries_consume_all_scripted_result_rows()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<AggregateContext>(AdoNetDialect.Ansi, options => new AggregateContext(options));
        database.Returns(FakeDatabase.Row("Value", 1), FakeDatabase.Row("Value", 2));

        var values = await context.Items.Where(item => item.Active).Select(item => item.Id)
            .Union(context.Items.Where(item => !item.Active).Select(item => item.Id))
            .OrderBy(id => id)
            .ToListAsync(Ct);

        Assert.Equal([1, 2], values);
        Assert.Contains("UNION", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AggregateItem
    {
        public int Id { get; set; }
        public bool Active { get; set; }
    }

    private sealed class AggregateContext(DbContextOptions<AggregateContext> options) : DbContext(options)
    {
        public DbSet<AggregateItem> Items => Set<AggregateItem>();
    }
}

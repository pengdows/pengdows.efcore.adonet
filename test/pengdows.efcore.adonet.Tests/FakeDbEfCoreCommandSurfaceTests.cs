using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Provider-neutral EF command surfaces executed end-to-end against scripted fakeDb results.</summary>
public sealed class FakeDbEfCoreCommandSurfaceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Raw_sql_scalar_queries_materialize_the_scripted_value()
    {
        var database = new FakeDatabase();
        await using var context = database.Context(AdoNetDialect.Ansi);
        database.Returns(FakeDatabase.Row("Value", 42));

        var value = await context.Database.SqlQueryRaw<int>("SELECT 42 AS \"Value\"").SingleAsync(Ct);

        Assert.Equal(42, value);
        Assert.Contains("SELECT 42", Assert.Single(database.Commands));
    }

    [Fact]
    public async Task From_sql_entity_queries_materialize_and_track_rows()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<SimpleContext>(AdoNetDialect.Ansi, options => new SimpleContext(options));
        database.Returns(
            new Dictionary<string, object?> { ["Id"] = 11, ["Active"] = true, ["Name"] = "from-sql" });

        var item = await context.Items.FromSqlRaw("SELECT \"Id\", \"Active\", \"Name\" FROM \"Items\"").SingleAsync(Ct);

        Assert.Equal((11, true, "from-sql"), (item.Id, item.Active, item.Name));
        Assert.Single(context.ChangeTracker.Entries<SimpleItem>());
    }

    [Fact]
    public async Task From_sql_interpolated_entity_queries_compose_and_materialize_from_fakeDb()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<SimpleContext>(AdoNetDialect.Ansi, options => new SimpleContext(options));
        database.Returns(
            new Dictionary<string, object?> { ["Id"] = 12, ["Active"] = true, ["Name"] = "interpolated" });

        var item = await context.Items
            .FromSqlInterpolated($"SELECT \"Id\", \"Active\", \"Name\" FROM \"Items\"")
            .Where(item => item.Active)
            .SingleAsync(Ct);

        Assert.Equal((12, true, "interpolated"), (item.Id, item.Active, item.Name));
        Assert.Contains("FROM (", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Interpolated_scalar_sql_queries_bind_parameters_and_materialize_results()
    {
        var database = new FakeDatabase();
        await using var context = database.Context(AdoNetDialect.Ansi);
        database.Returns(FakeDatabase.Row("Value", 27));
        var expected = 27;

        var value = await context.Database.SqlQuery<int>($"SELECT {expected} AS \"Value\"").SingleAsync(Ct);

        Assert.Equal(expected, value);
        Assert.Contains("p0", Assert.Single(database.Parameters), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_update_and_delete_return_provider_reported_row_counts()
    {
        var database = new FakeDatabase();
        await using var context = database.Context(AdoNetDialect.Ansi);
        database.Connection.EnqueueNonQueryResult(3);
        database.Connection.EnqueueNonQueryResult(2);

        var updated = await context.Items.Where(item => item.Active)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Name, "updated"), Ct);
        var deleted = await context.Items.Where(item => !item.Active).ExecuteDeleteAsync(Ct);

        Assert.Equal(3, updated);
        Assert.Equal(2, deleted);
        Assert.Contains("UPDATE", database.Commands[0]);
        Assert.Contains("DELETE", database.Commands[1]);
    }

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

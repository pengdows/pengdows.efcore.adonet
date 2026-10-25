using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Testing;

/// <summary>Bulk loading through whichever loader the dialect registers (native, or the core's multi-row INSERT).</summary>
public abstract class BulkConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (Fixture.SkipReason is { } skipReason)
        {
            Assert.Skip(skipReason);
        }

        if (Fixture.KnownEngineGaps.TryGetValue(TestContext.Current.Test!.TestCase.TestMethodName ?? "", out var reason))
        {
            Assert.Skip(reason);
        }

        await Fixture.ResetAsync();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private static List<Blog> Blogs(int count)
        => Enumerable.Range(0, count)
            .Select(i => new Blog
            {
                Name = $"bulk{i:D4}",
                IsActive = i % 3 == 0,
                Rating = i + 0.25m,
                CreatedAt = new DateTime(2026, 3, 4, 5, 6, 7).AddMinutes(i),
                ExternalId = new Guid(i + 1, 2, 3, [4, 5, 6, 7, 8, 9, 10, 11])
            })
            .ToList();

    [Fact]
    public async Task Bulk_insert_loads_every_row_and_value()
    {
        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(1500, await context.BulkInsertAsync(Blogs(1500), Ct));
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(1500, await context.Blogs.CountAsync(Ct));
            Assert.Equal(1500, await context.Blogs.Select(b => b.Id).Distinct().CountAsync(Ct));

            var blog = await context.Blogs.SingleAsync(b => b.Name == "bulk0999", Ct);
            // 999 is a multiple of 3: active.
            Assert.Equal((true, 999.25m, new DateTime(2026, 3, 4, 5, 6, 7).AddMinutes(999)), (blog.IsActive, blog.Rating, blog.CreatedAt));
            Assert.Equal(new Guid(1000, 2, 3, [4, 5, 6, 7, 8, 9, 10, 11]), blog.ExternalId);
            Assert.Equal(500, await context.Blogs.CountAsync(b => b.IsActive, Ct));
        }
    }

    [Fact]
    public async Task Bulk_insert_joins_the_current_transaction()
    {
        await using (var context = Fixture.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);
            await context.BulkInsertAsync(Blogs(10), Ct);
            Assert.Equal(10, await context.Blogs.CountAsync(Ct));
            await transaction.RollbackAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(0, await context.Blogs.CountAsync(Ct));
        }
    }

    [Fact]
    public async Task Bulk_insert_loads_rows_with_no_columns_to_write()
    {
        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(3, await context.BulkInsertAsync([new Tally(), new Tally(), new Tally()], Ct));
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(3, await context.Tallies.Select(t => t.Id).Distinct().CountAsync(Ct));
        }
    }

    [Fact]
    public void Bulk_insert_works_synchronously_too()
    {
        using (var context = Fixture.CreateContext())
        {
            Assert.Equal(20, context.BulkInsert(Blogs(20)));
        }

        using (var context = Fixture.CreateContext())
        {
            Assert.Equal(20, context.Blogs.Count());
        }
    }
}

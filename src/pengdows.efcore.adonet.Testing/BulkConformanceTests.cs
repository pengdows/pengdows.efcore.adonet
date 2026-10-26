using System.Data.Common;
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
        Fixture.SkipIfNotRunnable();

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

    private async Task<GaugeContext> GaugesAsync()
    {
        await Fixture.CreateTablesOnceAsync<GaugeContext>(o => new GaugeContext(o));
        await Fixture.ExecuteAsync($"DELETE FROM {Fixture.Quote("Gauges")}");
        await Fixture.ExecuteAsync($"DELETE FROM {Fixture.Quote("Readings")}");
        return Fixture.CreateContext<GaugeContext>(o => new GaugeContext(o));
    }

    /// <remarks>
    ///     A native loader must not bypass what an INSERT would enforce (SqlBulkCopy, for one, skips CHECK constraints unless
    ///     told otherwise). Runs on: every provider.
    /// </remarks>
    [Fact]
    public async Task Bulk_insert_checks_constraints()
    {
        await using (var context = await GaugesAsync())
        {
            var error = await Record.ExceptionAsync(() => context.BulkInsertAsync([new Gauge { Level = 11 }], Ct));
            Assert.True((error is DbException) || (error?.InnerException is DbException), $"Expected the database to refuse the row: {error}");
            await context.BulkInsertAsync([new Gauge { Level = 3 }], Ct);
        }

        await using (var context = Fixture.CreateContext<GaugeContext>(o => new GaugeContext(o)))
        {
            Assert.Equal([3], await context.Gauges.Select(g => g.Level).ToListAsync(Ct));
        }
    }

    /// <remarks>
    ///     A loader that writes each value as the type .NET would guess for it (a timestamp for a <see cref="DateTime" />)
    ///     fails or converts on a date column; it must write the column's type (Npgsql's binary COPY, for one, is strict).
    ///     Runs on: every provider.
    /// </remarks>
    [Fact]
    public async Task Bulk_insert_writes_values_as_the_column_type()
    {
        var day = new DateTime(2026, 5, 6);
        await using (var context = await GaugesAsync())
        {
            await context.BulkInsertAsync([new Reading { Day = day, Total = 7 }], Ct);
        }

        await using (var context = Fixture.CreateContext<GaugeContext>(o => new GaugeContext(o)))
        {
            var reading = await context.Readings.SingleAsync(Ct);
            Assert.Equal((day, 7L), (reading.Day, reading.Total));
        }
    }
}

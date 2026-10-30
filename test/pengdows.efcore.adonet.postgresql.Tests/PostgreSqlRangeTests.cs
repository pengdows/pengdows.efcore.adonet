using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NpgsqlTypes;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     A range is its own PostgreSQL type: it round-trips as the CLR range, its literal is the range text cast to its type, and a
///     range parameter matches its row. Arrays of ranges are not covered yet (EF's model finalization rejects them).
/// </summary>
public class PostgreSqlRangeTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private readonly string _connectionString = fixture.ConnectionStringFor("ranges_" + Guid.NewGuid().ToString("N")[..12]);

    private RangeContext Context()
    {
        var context = fixture.CreateContext<RangeContext>(o => new RangeContext(o));
        context.Database.SetConnectionString(_connectionString);
        return context;
    }

    public async ValueTask InitializeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureCreatedAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureDeletedAsync(Ct);
    }

    [Fact]
    public void Range_literals_are_the_range_text_cast_to_its_type()
    {
        using var context = fixture.CreateContext<RangeContext>(o => new RangeContext(o));
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(NpgsqlRange<int>), "int4range")!;

        Assert.Equal("'[1,5)'::int4range", mapping.GenerateSqlLiteral(new NpgsqlRange<int>(1, true, 5, false)));
        Assert.Equal("'(,10]'::int4range", mapping.GenerateSqlLiteral(new NpgsqlRange<int>(default, false, true, 10, true, false)));
    }

    [Fact]
    public async Task Ranges_round_trip_and_a_range_parameter_matches()
    {
        var range = new NpgsqlRange<int>(1, true, 5, false);
        await using (var context = Context())
        {
            context.Rows.Add(new RangeRow { Id = 1, Single = range });
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = Context();
        var row = await verify.Rows.AsNoTracking().SingleAsync(r => r.Id == 1, Ct);
        Assert.Equal(range, row.Single);

        var matched = await verify.Rows.AsNoTracking().Where(r => r.Single == range).Select(r => r.Id).SingleAsync(Ct);
        Assert.Equal(1, matched);
    }

    public sealed class RangeRow
    {
        public int Id { get; set; }
        public NpgsqlRange<int> Single { get; set; }
    }

    public sealed class RangeContext(DbContextOptions<RangeContext> options) : DbContext(options)
    {
        public DbSet<RangeRow> Rows => Set<RangeRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RangeRow>(e =>
            {
                e.Property(r => r.Single).HasColumnType("int4range");
            });
    }
}

/// <summary>
///     A DateTime over a timestamp with time zone column is an instant, read back in UTC, as Npgsql writes it.
/// </summary>
public class PostgreSqlTimestampWithTimeZoneTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private readonly string _connectionString = fixture.ConnectionStringFor("timestamptz_" + Guid.NewGuid().ToString("N")[..12]);

    private ZonedContext Context()
    {
        var context = fixture.CreateContext<ZonedContext>(o => new ZonedContext(o));
        context.Database.SetConnectionString(_connectionString);
        return context;
    }

    public async ValueTask InitializeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureCreatedAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureDeletedAsync(Ct);
    }

    [Fact]
    public async Task A_datetime_over_timestamp_with_time_zone_round_trips_as_utc()
    {
        var instant = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        await using (var context = Context())
        {
            context.Zoned.Add(new Zoned { Id = 1, At = instant });
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = Context();
        Assert.Equal(instant, await verify.Zoned.AsNoTracking().Where(z => z.Id == 1).Select(z => z.At).SingleAsync(Ct));
    }

    public sealed class Zoned
    {
        public int Id { get; set; }
        public DateTime At { get; set; }
    }

    public sealed class ZonedContext(DbContextOptions<ZonedContext> options) : DbContext(options)
    {
        public DbSet<Zoned> Zoned => Set<Zoned>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Zoned>(e => e.Property(z => z.At).HasColumnType("timestamp with time zone"));
    }
}

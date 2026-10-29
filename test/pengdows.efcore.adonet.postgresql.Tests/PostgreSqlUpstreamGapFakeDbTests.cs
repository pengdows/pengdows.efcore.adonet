using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NpgsqlTypes;
using System.Net;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>Failing parity probes for PostgreSQL array behavior present in Npgsql but not yet covered locally.</summary>
public sealed class PostgreSqlUpstreamGapFakeDbTests
{
    [Fact]
    public void Integer_array_contains_translates_to_postgresql_array_predicate()
    {
        var db = new FakePostgreSql();
        using var context = db.Context<ArrayContext>(options => new ArrayContext(options));

        var sql = context.Rows.Where(row => row.Values.Contains(3)).ToQueryString();

        Assert.Contains("ANY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inet_equality_uses_the_postgresql_network_mapping()
    {
        var db = new FakePostgreSql();
        using var context = db.Context<NetworkContext>(options => new NetworkContext(options));
        var address = IPAddress.Parse("192.0.2.1");

        var sql = context.Rows.Where(row => row.Address == address).ToQueryString();

        Assert.Contains("inet", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Non_utc_datetime_offset_values_are_rejected_before_execution()
    {
        var db = new FakePostgreSql();
        await using var context = db.Context<OffsetContext>(options => new OffsetContext(options));
        context.Rows.Add(new OffsetRow { Id = 1, Value = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(-8)) });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Empty(db.Commands);
    }

    [Fact]
    public void Integer_ranges_have_a_native_postgresql_type_mapping()
    {
        var db = new FakePostgreSql();
        using var context = db.Context<RangeContext>(options => new RangeContext(options));
        var mappings = context.GetService<IRelationalTypeMappingSource>();

        Assert.NotNull(mappings.FindMapping(typeof(NpgsqlRange<int>), "int4range"));
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public int[] Values { get; set; } = [];
    }

    private sealed class ArrayContext(DbContextOptions<ArrayContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().Property(row => row.Values).HasColumnType("integer[]");
    }

    private sealed class NetworkRow
    {
        public int Id { get; set; }
        public IPAddress Address { get; set; } = IPAddress.None;
    }

    private sealed class NetworkContext(DbContextOptions<NetworkContext> options) : DbContext(options)
    {
        public DbSet<NetworkRow> Rows => Set<NetworkRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NetworkRow>().Property(row => row.Address).HasColumnType("inet");
    }

    private sealed class OffsetRow
    {
        public int Id { get; set; }
        public DateTimeOffset Value { get; set; }
    }

    private sealed class OffsetContext(DbContextOptions<OffsetContext> options) : DbContext(options)
    {
        public DbSet<OffsetRow> Rows => Set<OffsetRow>();
    }

    private sealed class RangeRow
    {
        public int Id { get; set; }
        public NpgsqlRange<int> Bounds { get; set; }
    }

    private sealed class RangeContext(DbContextOptions<RangeContext> options) : DbContext(options)
    {
        public DbSet<RangeRow> Rows => Set<RangeRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RangeRow>().Property(row => row.Bounds).HasColumnType("int4range");
    }

}

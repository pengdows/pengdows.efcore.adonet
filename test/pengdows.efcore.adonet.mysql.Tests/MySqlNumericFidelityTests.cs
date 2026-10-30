using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     A decimal is DECIMAL(65,30), which holds every CLR decimal with its 28 places; a sbyte is TINYINT, a ulong BIGINT UNSIGNED
///     and a Guid CHAR(36). Each reads back as the CLR type it was written as, and a ulong parameter matches its row. MySQL's
///     bare DECIMAL is DECIMAL(10,0), which rounds the fraction away.
/// </summary>
public abstract class MySqlNumericFidelityTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : MySqlFixture
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private readonly string _connectionString = fixture.ConnectionStringFor("numeric_" + Guid.NewGuid().ToString("N")[..12]);

    private FidelityContext Context()
    {
        var context = fixture.CreateContext<FidelityContext>(o => new FidelityContext(o));
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

    // Round trips alone also pass through EF's converters, so the columns' own types are checked too. MariaDB spells the
    // integer types with a display width and UNSIGNED after it; MySQL 8.4 leaves the width out.
    [Fact]
    public async Task Columns_are_the_native_types_their_mappings_name()
    {
        await using var context = Context();
        var columns = await context.Database.SqlQueryRaw<string>(
                """
                SELECT CONCAT(COLUMN_NAME, ' ', COLUMN_TYPE) AS `Value` FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'rows'
                """)
            .ToListAsync(Ct);

        Assert.Contains("Amount decimal(65,30)", columns);
        Assert.Contains(columns, c => c.StartsWith("Small tinyint", StringComparison.Ordinal));
        Assert.Contains(columns, c => c.StartsWith("Big bigint", StringComparison.Ordinal) && c.EndsWith("unsigned", StringComparison.Ordinal));
        Assert.Contains("Key char(36)", columns);
    }

    [Fact]
    public async Task Decimal_values_round_trip_exactly_at_their_boundaries()
    {
        decimal[] values = [123456789.1234m, -987654321.4321m, 0.0000000000000000000000000001m, decimal.MaxValue, decimal.MinValue];
        await using (var context = Context())
        {
            context.Rows.AddRange(values.Select((value, i) => new Row { Id = i + 1, Amount = value }));
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = Context();
        var actual = await verify.Rows.AsNoTracking().OrderBy(r => r.Id).Select(r => r.Amount).ToListAsync(Ct);

        Assert.Equal(values, actual);
    }

    [Fact]
    public async Task Sbyte_and_ulong_round_trip_and_a_ulong_parameter_finds_its_row()
    {
        await using (var context = Context())
        {
            context.Rows.AddRange(
                new Row { Id = 1, Small = sbyte.MinValue, Big = ulong.MaxValue },
                new Row { Id = 2, Small = sbyte.MaxValue, Big = 1 });
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = Context();
        var all = await verify.Rows.AsNoTracking().OrderBy(r => r.Id).Select(r => new { r.Small, r.Big }).ToListAsync(Ct);
        Assert.Equal([(sbyte.MinValue, ulong.MaxValue), (sbyte.MaxValue, 1ul)], all.Select(r => (r.Small, r.Big)));

        var big = ulong.MaxValue;
        var id = await verify.Rows.AsNoTracking().Where(r => r.Big == big).Select(r => r.Id).SingleAsync(Ct);
        Assert.Equal(1, id);
    }

    [Fact]
    public async Task Guid_values_round_trip_as_guids()
    {
        var guid = Guid.NewGuid();
        await using (var context = Context())
        {
            context.Rows.Add(new Row { Id = 1, Key = guid });
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = Context();
        var key = await verify.Rows.AsNoTracking().Where(r => r.Key == guid).Select(r => r.Key).SingleAsync(Ct);
        Assert.Equal(guid, key);
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public sbyte Small { get; set; }
        public ulong Big { get; set; }
        public Guid Key { get; set; }
    }

    public sealed class FidelityContext(DbContextOptions<FidelityContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>(e => e.Property(r => r.Id).ValueGeneratedNever());
    }
}

public class MySqlConnectorNumericFidelityTests(MySqlFixture fixture) : MySqlNumericFidelityTests<MySqlFixture>(fixture);

public class MariaDbNumericFidelityTests(MariaDbFixture fixture) : MySqlNumericFidelityTests<MariaDbFixture>(fixture);

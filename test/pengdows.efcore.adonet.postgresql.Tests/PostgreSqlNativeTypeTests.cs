using System.Collections;
using System.Net;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using pengdows.efcore.adonet.postgresql;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     PostgreSQL types the provider binds by name: a value PostgreSQL won't take as text is sent as its CLR type with the
///     parameter naming the column's type. A collection of such a type (or of a range) is a model property, so it must build
///     without a store type, and a model property with a store type must write and read back the same value.
/// </summary>
public class PostgreSqlNativeTypeTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public void Collections_of_provider_types_build_a_model_with_or_without_a_store_type()
    {
        var options = new DbContextOptionsBuilder<NativeContext>().UseAdoNetPostgreSql(fixture.ConnectionString).Options;
        using var context = new NativeContext(options);

        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(NativeRow)));
    }

    [Fact]
    public async Task Points_bit_strings_and_network_values_round_trip_as_scalars_and_arrays()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<NativeContext>().UseAdoNetPostgreSql(fixture.ConnectionString).Options;
        await using (var setup = new NativeContext(options))
        {
            await setup.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS native_round_trip", ct);
            await setup.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE native_round_trip (
                    id integer PRIMARY KEY,
                    point_value point NOT NULL,
                    points_value point[] NOT NULL,
                    bits_value bit varying NOT NULL,
                    network_value cidr NOT NULL)
                """, ct);
        }

        var row = new NativeRow
        {
            Id = 1,
            Point = new NpgsqlPoint(1.5, 2.5),
            Points = [new NpgsqlPoint(0, 0), new NpgsqlPoint(3, 4)],
            Bits = new BitArray([true, false, true]),
            Network = new IPNetwork(IPAddress.Parse("10.1.2.0"), 24),
        };
        await using (var write = new NativeContext(options))
        {
            write.Rows.Add(row);
            await write.SaveChangesAsync(ct);
        }

        await using var read = new NativeContext(options);
        var actual = await read.Rows.AsNoTracking().SingleAsync(r => r.Id == 1, ct);

        Assert.Equal(row.Point, actual.Point);
        Assert.Equal(row.Points, actual.Points);
        Assert.Equal(new[] { true, false, true }, actual.Bits!.Cast<bool>());
        Assert.Equal(row.Network, actual.Network);
    }

    public sealed class NativeRow
    {
        public int Id { get; set; }
        public NpgsqlPoint Point { get; set; }
        public NpgsqlPoint[] Points { get; set; } = [];
        public BitArray? Bits { get; set; }
        public IPNetwork Network { get; set; }
    }

    public sealed class NativeContext(DbContextOptions<NativeContext> options) : DbContext(options)
    {
        public DbSet<NativeRow> Rows => Set<NativeRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NativeRow>(e =>
            {
                e.ToTable("native_round_trip");
                e.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
                e.Property(r => r.Point).HasColumnName("point_value").HasColumnType("point");
                e.Property(r => r.Points).HasColumnName("points_value").HasColumnType("point[]");
                e.Property(r => r.Bits).HasColumnName("bits_value").HasColumnType("bit varying");
                e.Property(r => r.Network).HasColumnName("network_value").HasColumnType("cidr");
            });
    }
}

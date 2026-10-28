using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     MySQL's model options, as Pomelo has them: HasCharSet on a property is its column's CHARACTER SET, on an entity type
///     its table's default (which its columns take), and on the model every table's default; HasPrefixLength indexes only a
///     column's first characters. Read back from information_schema. Provider-only: character sets and index prefixes are
///     MySQL's. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlPropertyBuilderExtensions.cs
/// </summary>
public abstract class MySqlCharSetAndIndexTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : MySqlFixture
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private readonly string _database = "charset_" + Guid.NewGuid().ToString("N")[..12];

    private ShelfContext Context()
    {
        var context = fixture.CreateContext<ShelfContext>(
            o => new ShelfContext(o), o => pengdows.efcore.adonet.NetTopologySuite.AdoNetNetTopologySuiteExtensions.UseNetTopologySuite(o));
        context.Database.SetConnectionString(fixture.ConnectionStringFor(_database));
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

    private async Task<string?> Scalar(string sql)
    {
        await using var context = Context();
        return await context.Database.SqlQueryRaw<string>(sql, _database).SingleAsync(Ct);
    }

    [Fact]
    public async Task Character_sets_go_to_the_column_the_table_and_every_table()
    {
        Assert.Equal("latin1", await Scalar(
            "SELECT CHARACTER_SET_NAME AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = {0} AND TABLE_NAME = 'Shelves' AND COLUMN_NAME = 'Code'"));
        Assert.Equal("ascii", await Scalar(
            "SELECT CHARACTER_SET_NAME AS `Value` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = {0} AND TABLE_NAME = 'Shelves' AND COLUMN_NAME = 'Label'"));
        Assert.StartsWith("ascii_", await Scalar(
            "SELECT TABLE_COLLATION AS `Value` FROM information_schema.TABLES WHERE TABLE_SCHEMA = {0} AND TABLE_NAME = 'Shelves'"));
        Assert.StartsWith("utf8mb4_", await Scalar(
            "SELECT TABLE_COLLATION AS `Value` FROM information_schema.TABLES WHERE TABLE_SCHEMA = {0} AND TABLE_NAME = 'Books'"));
    }

    [Fact]
    public async Task A_prefix_length_indexes_the_columns_first_characters()
        => Assert.Equal("10", await Scalar(
            "SELECT CAST(SUB_PART AS CHAR) AS `Value` FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = {0} AND TABLE_NAME = 'Books' AND INDEX_NAME = 'IX_Books_Title'"));

    [Fact]
    public async Task A_spatial_index_is_spatial()
        => Assert.Equal("SPATIAL", await Scalar(
            "SELECT INDEX_TYPE AS `Value` FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = {0} AND TABLE_NAME = 'Places' AND INDEX_NAME = 'IX_Places_Location'"));

    public sealed class Place
    {
        public int Id { get; set; }
        public global::NetTopologySuite.Geometries.Point Location { get; set; } = null!;
    }

    public sealed class Shelf
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public sealed class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    public sealed class ShelfContext(DbContextOptions<ShelfContext> options) : DbContext(options)
    {
        public DbSet<Shelf> Shelves => Set<Shelf>();
        public DbSet<Book> Books => Set<Book>();
        public DbSet<Place> Places => Set<Place>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4");
            modelBuilder.Entity<Shelf>(b =>
            {
                b.HasCharSet("ascii");
                b.Property(s => s.Code).HasMaxLength(20).HasCharSet("latin1");
                b.Property(s => s.Label).HasMaxLength(20);
            });
            modelBuilder.Entity<Book>(b =>
            {
                b.Property(s => s.Title).HasMaxLength(300);
                b.HasIndex(s => s.Title).HasPrefixLength(10);
            });
            modelBuilder.Entity<Place>().HasIndex(p => p.Location).IsSpatial();
        }
    }
}

public class MySqlConnectorCharSetAndIndexTests(MySqlFixture fixture) : MySqlCharSetAndIndexTests<MySqlFixture>(fixture);

public class MariaDbCharSetAndIndexTests(MariaDbFixture fixture) : MySqlCharSetAndIndexTests<MariaDbFixture>(fixture);

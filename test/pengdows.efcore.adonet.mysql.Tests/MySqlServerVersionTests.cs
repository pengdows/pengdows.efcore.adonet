using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NetTopologySuite.Geometries;
using Microsoft.Extensions.DependencyInjection;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.NetTopologySuite;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     A dialect for an older server turns off what it lacks, as Pomelo's ServerVersion support flags do: LATERAL and outer
///     references in derived tables (MySQL 8.0.14), window functions (MySQL 8.0, MariaDB 10.2), JSON_TABLE for primitive
///     collections (MySQL 8.0.4, MariaDB 10.6), JSON_EQUALS (MariaDB 10.7), the VALUES table constructor (MySQL 8.0.19's VALUES
///     ROW, MariaDB 10.3.3), REGEXP_LIKE and REGEXP_REPLACE (MySQL 8.0.4), ST_GeomFromText's axis order (MySQL 8.0.1), expression
///     defaults (MySQL 8.0.13, MariaDB 10.2.7), a column's SRID (MySQL 8.0.3), DESC indexes (MySQL 8.0.1) and RENAME COLUMN
///     (MySQL 8.0, MariaDB 10.5.2). A current server keeps the dialects it had. SQL shape and fakeDb. Provider-only. Behaviour
///     reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Infrastructure/MySqlServerVersion.cs
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Infrastructure/MariaDbServerVersion.cs
/// </summary>
public class MySqlServerVersionTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class Blog
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public string Name { get; set; } = "";
        public string Body { get; set; } = "";
        public string Tags { get; set; } = "[]";
        public List<Post> Posts { get; set; } = [];
    }

    public sealed class Post
    {
        public int Id { get; set; }
        public int BlogId { get; set; }
        public string Title { get; set; } = "";
    }

    public sealed class Place
    {
        public int Id { get; set; }
        public Point Location { get; set; } = null!;
    }

    public sealed class BlogContext(DbContextOptions<BlogContext> options) : DbContext(options)
    {
        public DbSet<Blog> Blogs => Set<Blog>();
        public DbSet<Post> Posts => Set<Post>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>(b =>
            {
                b.Property(x => x.Name).HasMaxLength(50).HasDefaultValueSql("'untitled'");
                b.Property(x => x.Tags).HasColumnType("json");
                b.HasIndex(x => x.Name).IsDescending();
            });
        }
    }

    public sealed class PlaceContext(DbContextOptions<PlaceContext> options) : DbContext(options)
    {
        public DbSet<Place> Places => Set<Place>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Place>().Property(p => p.Location).HasSpatialReferenceSystem(4326);
    }

    // Each version is a dialect of its own, and EF builds an internal service provider for each; built here, one per dialect,
    // they stay out of EF's shared cache, which counts providers across the whole test run.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<MySqlDialect, IServiceProvider> Providers = new();

    private static IServiceProvider Provider(MySqlDialect dialect)
        => Providers.GetOrAdd(
            dialect,
            d => new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddEntityFrameworkAdoNet(d)
                .AddEntityFrameworkAdoNetNetTopologySuite()
                .BuildServiceProvider());

    private static DbContextOptionsBuilder<TContext> Options<TContext>(MySqlDialect dialect)
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>().AllowManyDialects()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(dialect).UseNetTopologySuite())
            .UseInternalServiceProvider(Provider(dialect));

    private static BlogContext Context(MySqlDialect dialect)
        => new(Options<BlogContext>(dialect).Options);

    private static MySqlDialect MySql(int major, int minor, int build)
        => MySqlDialect.ForServerVersion(new Version(major, minor, build));

    private static MySqlDialect MariaDb(int major, int minor, int build)
        => MySqlDialect.ForServerVersion(new Version(major, minor, build), mariaDb: true);

    [Theory]
    [InlineData("8.4.3", false, null)]
    [InlineData("8.0.36-log", false, null)]
    [InlineData("8.0.13", false, "8.0.13")]
    [InlineData("5.7.44-log", false, "5.7.44")]
    [InlineData("11.4.2-MariaDB-ubu2404", true, null)]
    [InlineData("5.5.5-10.11.6-MariaDB", true, null)]
    [InlineData("5.5.5-10.5.22-MariaDB", true, "10.5.22")]
    [InlineData("10.3.39-MariaDB-1:10.3.39+maria~ubu2004", true, "10.3.39")]
    public void From_server_version_gives_a_current_server_its_dialect_and_an_older_one_its_versions(
        string handshake,
        bool mariaDb,
        string? olderVersion)
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=MySql" };
        connection.SetServerVersion(handshake);
        connection.Open();

        var dialect = MySqlDialect.FromServerVersion(connection);

        if (olderVersion == null)
        {
            Assert.Same(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance, dialect);
            return;
        }

        Assert.Equal(Version.Parse(olderVersion), dialect.ServerVersion);
        Assert.Equal(mariaDb ? "MariaDB" : "MySQL", dialect.Name);
        Assert.Same(dialect, MySqlDialect.ForServerVersion(Version.Parse(olderVersion), mariaDb));
    }

    [Fact]
    public void A_server_older_than_its_json_is_refused()
    {
        Assert.Throws<NotSupportedException>(() => MySql(5, 7, 7));
        Assert.Throws<NotSupportedException>(() => MariaDb(10, 2, 3));
    }

    [Fact]
    public void Lateral_joins_and_outer_references_from_MySql_8_0_14()
    {
        Assert.Null(MySql(8, 0, 13).Sql.LateralJoins);
        Assert.False(MySql(8, 0, 13).Sql.SupportsOuterReferencesInDerivedTables);
        Assert.NotNull(MySql(8, 0, 14).Sql.LateralJoins);
        using var context = Context(MySql(8, 0, 13));

        Assert.ThrowsAny<InvalidOperationException>(
            () => context.Blogs.SelectMany(b => context.Posts.Where(p => p.Id > b.Id).OrderBy(p => p.Id).Take(1), (b, p) => new { b.Id, p.Title })
                .ToQueryString());
    }

    [Theory]
    [InlineData(5, 7, 44, false, false)]
    [InlineData(8, 0, 0, false, true)]
    [InlineData(10, 2, 4, true, true)]
    public void Window_functions_from_MySql_8_and_MariaDb_10_2(int major, int minor, int build, bool mariaDb, bool supported)
    {
        var dialect = MySqlDialect.ForServerVersion(new Version(major, minor, build), mariaDb);
        if (!dialect.Sql.SupportsWindowFunctions)
        {
            Assert.False(supported);
            using var context = Context(dialect);
            Assert.ThrowsAny<InvalidOperationException>(
                () => context.Posts.GroupBy(p => p.BlogId).Select(g => g.OrderBy(p => p.Id).First()).ToQueryString());
            return;
        }

        Assert.True(supported);
    }

    [Theory]
    [InlineData(8, 0, 3, false, false)]
    [InlineData(8, 0, 4, false, true)]
    [InlineData(10, 5, 22, true, false)]
    [InlineData(10, 6, 0, true, true)]
    public void Json_table_from_MySql_8_0_4_and_MariaDb_10_6(int major, int minor, int build, bool mariaDb, bool supported)
        => Assert.Equal(supported, MySqlDialect.ForServerVersion(new Version(major, minor, build), mariaDb).Sql.Json!.ArrayElements != null);

    // Before JSON_EQUALS, documents are compared compacted: whitespace no longer differs, though a different key order does.
    [Fact]
    public void Json_equality_on_MariaDb_before_10_7_compares_compacted_documents()
    {
        Assert.Equal("JSON_COMPACT({0}) = JSON_COMPACT({1})", MariaDb(10, 6, 0).Sql.Json!.Equality);
        Assert.Equal("JSON_EQUALS({0}, {1})", MariaDb(10, 7, 0).Sql.Json!.Equality);
    }

    // Before VALUES ROW, each row of an inline collection is its own SELECT.
    [Theory]
    [InlineData(8, 0, 18, false, false)]
    [InlineData(8, 0, 19, false, true)]
    [InlineData(10, 3, 2, true, false)]
    [InlineData(10, 3, 3, true, true)]
    public void A_table_value_constructor_from_MySql_8_0_19_and_MariaDb_10_3_3(int major, int minor, int build, bool mariaDb, bool supported)
    {
        using var context = Context(MySqlDialect.ForServerVersion(new Version(major, minor, build), mariaDb));

        var sql = context.Blogs.Select(b => new[] { 1, 2, 3 }.Count(x => x > b.Id)).ToQueryString();

        Assert.Equal(supported, sql.Contains("VALUES", StringComparison.Ordinal));
        Assert.Equal(!supported, sql.Contains("UNION ALL SELECT", StringComparison.Ordinal));
    }

    // MySQL 5.7's REGEXP is case-sensitive against a binary string, and it has no REGEXP_REPLACE for a character-set trim.
    [Fact]
    public void Regular_expressions_from_MySql_8_0_4()
    {
        Assert.Equal("CASE WHEN {1} = '' THEN TRUE ELSE {0} REGEXP BINARY {1} END", MySql(8, 0, 3).Sql.Functions.RegexIsMatch);
        Assert.Null(MySql(8, 0, 3).Sql.Functions.TrimCharacters);
        Assert.Contains("REGEXP_LIKE", MySql(8, 0, 4).Sql.Functions.RegexIsMatch);
    }

    [Fact]
    public void Geometry_text_takes_an_axis_order_from_MySql_8_0_1()
    {
        Assert.Equal("ST_GeomFromText({0}, {1})", MySql(8, 0, 0).Sql.Spatial!.FromText);
        Assert.Contains("axis-order", MySql(8, 0, 1).Sql.Spatial!.FromText);
    }

    private static string Ddl(MySqlDialect dialect, params MigrationOperation[] operations)
    {
        using var context = Context(dialect);
        return string.Join("", context.GetService<IMigrationsSqlGenerator>()
            .Generate(operations, context.GetService<IDesignTimeModel>().Model).Select(c => c.CommandText));
    }

    private static string CreateScript(MySqlDialect dialect)
    {
        using var context = Context(dialect);
        return context.Database.GenerateCreateScript();
    }

    // Before expression defaults, a default is a literal or CURRENT_TIMESTAMP, written bare; a TEXT, BLOB or JSON column has
    // none.
    [Fact]
    public void Expression_defaults_from_MySql_8_0_13()
    {
        Assert.Contains("`Name` VARCHAR(50) DEFAULT 'untitled' NOT NULL", CreateScript(MySql(8, 0, 12)));
        Assert.Contains("DEFAULT ('untitled')", CreateScript(MySql(8, 0, 13)));
        Assert.Throws<NotSupportedException>(() => Ddl(
            MySql(8, 0, 12),
            new AddColumnOperation { Table = "Blogs", Name = "Notes", ClrType = typeof(string), ColumnType = "longtext", DefaultValue = "x" }));
    }

    private static string PlacesScript(MySqlDialect dialect)
    {
        using var context = new PlaceContext(Options<PlaceContext>(dialect).Options);
        return context.Database.GenerateCreateScript();
    }

    [Fact]
    public void A_column_srid_from_MySql_8_0_3()
    {
        Assert.Throws<NotSupportedException>(() => PlacesScript(MySql(8, 0, 2)));
        Assert.Contains("SRID 4326", PlacesScript(MySql(8, 0, 3)));
    }

    [Fact]
    public void Descending_indexes_from_MySql_8_0_1()
    {
        Assert.Contains("(`Name`);", CreateScript(MySql(8, 0, 0)));
        Assert.Contains("(`Name` DESC);", CreateScript(MySql(8, 0, 1)));
    }

    // Before RENAME COLUMN, CHANGE COLUMN restates the column, from the migration's target model.
    [Theory]
    [InlineData(5, 7, 44, false)]
    [InlineData(10, 4, 34, true)]
    public void Rename_column_before_mysql_8_and_MariaDb_10_5_2_changes_the_column(int major, int minor, int build, bool mariaDb)
    {
        var sql = Ddl(
            MySqlDialect.ForServerVersion(new Version(major, minor, build), mariaDb),
            new RenameColumnOperation { Table = "Blogs", Name = "Title", NewName = "Name" });

        Assert.Contains("ALTER TABLE `Blogs` CHANGE COLUMN `Title` `Name` varchar(50)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RENAME COLUMN", Ddl(MySql(8, 0, 0), new RenameColumnOperation { Table = "Blogs", Name = "Title", NewName = "Name" }));
    }

    // A query a 5.7 dialect writes runs: an inline collection as SELECT rows, read back through fakeDb.
    [Fact]
    public async Task An_older_dialects_query_runs_through_fakeDb()
    {
        var db = new FakeMySql();
        await using var context = db.Context<BlogContext>(
            o => new BlogContext(o),
            adoNet: o => o.UseDialect(MySql(5, 7, 44)).UseNetTopologySuite(),
            // Its own provider: sensitive-data logging must be the same for every context on one.
            options: o => o.UseInternalServiceProvider(
                new ServiceCollection().AddEntityFrameworkAdoNet(MySql(5, 7, 44)).AddEntityFrameworkAdoNetNetTopologySuite().BuildServiceProvider()));
        db.Returns([("Count", 9)]);

        var count = await context.Blogs.Select(b => new[] { 1, 2, 3 }.Count(x => x > b.Id)).SingleAsync(Ct);

        Assert.Equal(9, count);
        Assert.Contains("UNION ALL SELECT", db.Commands.Single());
        Assert.DoesNotContain("VALUES", db.Commands.Single());
    }
}

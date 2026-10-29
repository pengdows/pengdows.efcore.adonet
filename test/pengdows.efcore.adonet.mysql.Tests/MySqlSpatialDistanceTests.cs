using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using pengdows.efcore.adonet.mysql.NetTopologySuite;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Pomelo's spatial EF.Functions, from the MySQL NetTopologySuite package: SpatialDistancePlanar (MySQL 8 measures a
///     geographic SRID's distance on the ellipsoid, so the SRID is set to 0 first; MariaDB measures planar whatever the SRID) and
///     SpatialDistanceSphere by its native function, Andoyer's formula (MySQL's ST_Distance on SRID 4326; written out on
///     MariaDB, which has none) or the haversine. SQL shape and fakeDb. Provider-only. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.NTS/Query/ExpressionTranslators/Internal/MySqlDbFunctionsExtensionsMethodTranslator.cs
/// </summary>
public class MySqlSpatialDistanceTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class Place
    {
        public int Id { get; set; }
        public Point Location { get; set; } = null!;
    }

    public sealed class PlaceContext(DbContextOptions<PlaceContext> options) : DbContext(options)
    {
        public DbSet<Place> Places => Set<Place>();
    }

    private static PlaceContext Context(bool mariaDb)
        => new(new DbContextOptionsBuilder<PlaceContext>().AllowManyDialects()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance).UseMySqlNetTopologySuite())
            .Isolated("mysql-nts " + mariaDb)
            .Options);

    private static readonly Point Oslo = new(10.75, 59.91) { SRID = 4326 };

    private static string Sql(bool mariaDb, SpatialDistanceAlgorithm? algorithm)
    {
        using var context = Context(mariaDb);
        var point = Oslo;
        // The algorithm is a constant in each query, as it must be.
        return algorithm switch
        {
            SpatialDistanceAlgorithm.Native
                => context.Places.Where(p => EF.Functions.SpatialDistanceSphere(p.Location, point, SpatialDistanceAlgorithm.Native) < 1000).ToQueryString(),
            SpatialDistanceAlgorithm.Andoyer
                => context.Places.Where(p => EF.Functions.SpatialDistanceSphere(p.Location, point, SpatialDistanceAlgorithm.Andoyer) < 1000).ToQueryString(),
            SpatialDistanceAlgorithm.Haversine
                => context.Places.Where(p => EF.Functions.SpatialDistanceSphere(p.Location, point, SpatialDistanceAlgorithm.Haversine) < 1000).ToQueryString(),
            _ => context.Places.Where(p => EF.Functions.SpatialDistancePlanar(p.Location, point) < 1000).ToQueryString()
        };
    }

    [Fact]
    public void Planar_distance_on_MySql_sets_a_geographic_srid_to_0()
        => Assert.Contains(
            "CASE WHEN ST_SRID(`p`.`Location`) = 0 THEN ST_Distance(`p`.`Location`, @point) ELSE ST_Distance(ST_SRID(`p`.`Location`, 0), ST_SRID(@point, 0)) END",
            Sql(mariaDb: false, null));

    [Fact]
    public void Planar_distance_on_MariaDb_is_its_distance()
        => Assert.Contains("ST_Distance(`p`.`Location`, @point) < 1000", Sql(mariaDb: true, null));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sphere_distance_by_the_native_function(bool mariaDb)
        => Assert.Contains("ST_Distance_Sphere(`p`.`Location`, @point)", Sql(mariaDb, SpatialDistanceAlgorithm.Native));

    [Fact]
    public void Andoyer_on_MySql_is_its_distance_on_srid_4326()
        => Assert.Contains(
            "CASE WHEN ST_SRID(`p`.`Location`) = 4326 THEN ST_Distance(`p`.`Location`, @point) ELSE ST_Distance(ST_SRID(`p`.`Location`, 4326), ST_SRID(@point, 4326)) END",
            Sql(mariaDb: false, SpatialDistanceAlgorithm.Andoyer));

    // MariaDB has no ellipsoidal distance: Andoyer's formula on the WGS 84 ellipsoid, in SQL.
    [Fact]
    public void Andoyer_on_MariaDb_is_the_formula()
    {
        var sql = Sql(mariaDb: true, SpatialDistanceAlgorithm.Andoyer);

        Assert.Contains("6378137", sql);
        Assert.Contains("ATAN(SQRT(", sql);
        Assert.Contains("ST_Y(`p`.`Location`)", sql);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Haversine_is_the_formula(bool mariaDb, bool setsSridTo0)
    {
        var sql = Sql(mariaDb, SpatialDistanceAlgorithm.Haversine);

        Assert.Contains("6370986", sql);
        Assert.Contains("ASIN(SQRT(", sql);
        Assert.Equal(setsSridTo0, sql.Contains("ST_SRID(`p`.`Location`, 0)", StringComparison.Ordinal));
    }

    // Pomelo needs the algorithm as a constant, to choose the SQL.
    [Fact]
    public void An_algorithm_known_only_at_run_time_is_refused()
    {
        using var context = Context(mariaDb: false);
        var algorithm = SpatialDistanceAlgorithm.Haversine;

        Assert.Throws<InvalidOperationException>(
            () => context.Places.Where(p => EF.Functions.SpatialDistanceSphere(p.Location, Oslo, algorithm) < 1).ToQueryString());
    }

    [Fact]
    public async Task Distances_read_back_through_fakeDb()
    {
        var db = new FakeMySql();
        await using var context = db.Context<PlaceContext>(o => new PlaceContext(o), adoNet: o => o.UseMySqlNetTopologySuite());
        var point = Oslo;
        db.Returns([("Planar", 12.5), ("Sphere", 1234.5)]);

        var row = await context.Places
            .Select(p => new
            {
                Planar = EF.Functions.SpatialDistancePlanar(p.Location, point),
                Sphere = EF.Functions.SpatialDistanceSphere(p.Location, point, SpatialDistanceAlgorithm.Native)
            })
            .SingleAsync(Ct);

        Assert.Equal((12.5, 1234.5), (row.Planar, row.Sphere));
        Assert.Contains("ST_Distance_Sphere(`p`.`Location`, @point)", db.Commands.Single());
        Assert.Contains("@point=", db.Parameters.Single());
    }

    [Fact]
    public void The_functions_throw_outside_a_query()
        => Assert.Throws<InvalidOperationException>(() => EF.Functions.SpatialDistancePlanar(Oslo, Oslo));
}

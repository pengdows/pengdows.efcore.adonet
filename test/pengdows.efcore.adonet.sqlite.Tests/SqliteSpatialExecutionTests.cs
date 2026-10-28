using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using pengdows.efcore.adonet.NetTopologySuite;
using static pengdows.efcore.adonet.sqlite.Tests.SqliteSpatialTests;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     SpatiaLite through fakeDb emulating SQLite: geometries are written as GAIA blob parameters and scripted GAIA blobs read
///     back as geometries; the spatial DDL is executed as migrations would send it. Loading mod_spatialite needs a
///     Microsoft.Data.Sqlite connection, which is the integration suites'.
/// </summary>
public class SqliteSpatialExecutionTests
{
    private readonly FakeSqlite _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private PlaceContext Context()
        => _db.Context<PlaceContext>(o => new PlaceContext(o), o => o.UseNetTopologySuite());

    private static byte[] Gaia(Geometry geometry, Ordinates ordinates = Ordinates.XY)
        => new GaiaGeoWriter { HandleOrdinates = ordinates }.Write(geometry);

    [Fact]
    public async Task A_geometry_is_sent_as_a_gaia_blob_with_its_srid()
    {
        await using var context = Context();
        _db.Returns([5]);
        var place = new Place { Location = new Point(1.5, -2.25) { SRID = 4326 } };

        context.Add(place);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(5, place.Id);
        Assert.Contains("INSERT INTO \"Places\"", Assert.Single(_db.Commands));
        // A GAIA blob: 0x00 start, then 0x01 (little-endian), then the SRID 4326 as E6100000.
        Assert.Contains("='0x0001E6100000", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task A_scripted_gaia_blob_reads_back_as_the_geometry()
    {
        await using var context = Context();
        var polygon = new Polygon(new LinearRing(
            [new CoordinateZ(0, 0, 1), new CoordinateZ(0, 1, 2), new CoordinateZ(1, 1, 3), new CoordinateZ(0, 0, 1)])) { SRID = 3857 };
        _db.Returns([1, Gaia(polygon, Ordinates.XYZ), Gaia(new Point(3, 4) { SRID = 4326 })]);

        var place = await context.Places.SingleAsync(Ct);

        Assert.Equal((3.0, 4.0, 4326), (place.Location.X, place.Location.Y, place.Location.SRID));
        Assert.Equal(3857, place.Area!.SRID);
        Assert.Equal(2, place.Area.Coordinates[1].Z);
    }

    [Fact]
    public async Task A_spatial_function_reads_back_its_value()
    {
        await using var context = Context();
        var other = new Point(0, 0) { SRID = 4326 };
        _db.Returns([12.5]);

        Assert.Equal(12.5, await context.Places.Select(p => p.Location.Distance(other)).SingleAsync(Ct));
        Assert.Contains("Distance(\"p\".\"Location\", @other)", Assert.Single(_db.Commands));
        Assert.Contains("@other='0x0001E6100000", Assert.Single(_db.Parameters));
    }

    [Fact]
    public void The_spatial_ddl_is_executed_with_the_catalog_first()
    {
        using var context = Context();

        context.GetService<IRelationalDatabaseCreator>().CreateTables();

        var sent = string.Join("\n", _db.Commands);
        Assert.Contains("SELECT InitSpatialMetaData()", sent);
        Assert.Contains("SELECT AddGeometryColumn('Places', 'Location', 4326, 'POINT', -1, 1)", sent);
        Assert.True(sent.IndexOf("InitSpatialMetaData", StringComparison.Ordinal) < sent.IndexOf("CREATE TABLE", StringComparison.Ordinal));
    }
}

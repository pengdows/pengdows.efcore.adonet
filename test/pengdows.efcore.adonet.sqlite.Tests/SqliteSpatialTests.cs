using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using pengdows.efcore.adonet.NetTopologySuite;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     SpatiaLite through the shared NetTopologySuite package, as Microsoft's SQLite NetTopologySuite provider: geometry columns
///     hold SpatiaLite's GAIA blob, are added with AddGeometryColumn after their table (SpatiaLite registers them in its
///     catalog), the catalog is made with InitSpatialMetaData, and members are SpatiaLite's functions. SQL shape only.
///     Behaviour reference (not copied): https://github.com/dotnet/efcore/tree/v10.0.11/src/EFCore.Sqlite.NTS
/// </summary>
public class SqliteSpatialTests
{
    private static PlaceContext Context()
        => new(new DbContextOptionsBuilder<PlaceContext>().UseAdoNetSqlite("Data Source=:memory:", o => o.UseNetTopologySuite()).Options);

    private static string Sql<T>(Func<IQueryable<Place>, IQueryable<T>> query)
    {
        using var context = Context();
        return query(context.Places).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void Geometry_columns_are_added_after_their_table_with_their_srid()
    {
        using var context = Context();
        var script = context.Database.GenerateCreateScript().ReplaceLineEndings("\n");

        Assert.Contains("SELECT InitSpatialMetaData();", script);
        Assert.DoesNotContain("\"Location\" POINT", script);
        Assert.Contains("SELECT AddGeometryColumn('Places', 'Location', 4326, 'POINT', -1, 1);", script);
        Assert.Contains("SELECT AddGeometryColumn('Places', 'Area', 0, 'POLYGONZ', -1, 0);", script);
        Assert.True(script.IndexOf("InitSpatialMetaData", StringComparison.Ordinal) < script.IndexOf("CREATE TABLE", StringComparison.Ordinal));
    }

    [Fact]
    public void A_geometry_column_added_later_is_added_with_add_geometry_column()
    {
        using var context = Context();
        var sql = string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate(
        [
            new AddColumnOperation
            {
                Table = "we'ird", Name = "Shape", ClrType = typeof(Geometry), ColumnType = "GEOMETRY", IsNullable = true,
                ["Sqlite:Srid"] = 3857
            }
        ]).Select(c => c.CommandText));

        Assert.Contains("SELECT AddGeometryColumn('we''ird', 'Shape', 3857, 'GEOMETRY', -1, 0);", sql);
        Assert.DoesNotContain("ALTER TABLE", sql);
    }

    [Fact]
    public void A_geometry_column_with_the_legacy_srid_annotation_is_added_with_that_srid()
    {
        using var context = Context();
        var sql = string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate(
        [
            new AddColumnOperation
            {
                Table = "places", Name = "Shape", ClrType = typeof(Geometry), ColumnType = "GEOMETRY", IsNullable = true,
                ["AdoNet:Sqlite:Srid"] = 3857
            }
        ]).Select(c => c.CommandText));

        Assert.Contains("SELECT AddGeometryColumn('places', 'Shape', 3857, 'GEOMETRY', -1, 0);", sql);
    }

    // A geometry mapping converts through its SpatialConverter, which EF keeps protected; values are bound through it.
    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter Converter(RelationalTypeMapping mapping)
        => (Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)mapping.GetType()
            .GetProperty("SpatialConverter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(mapping)!;

    [Fact]
    public void A_geometry_round_trips_through_the_gaia_blob_with_its_srid_and_z()
    {
        using var context = Context();
        var source = context.GetService<IRelationalTypeMappingSource>();
        var point = new Point(1.5, -2.25, 3) { SRID = 4326 };
        var mapping = source.FindMapping(typeof(Polygon), "POLYGONZ")!;
        var polygon = new Polygon(new LinearRing([new CoordinateZ(0, 0, 1), new CoordinateZ(0, 1, 1), new CoordinateZ(1, 1, 1), new CoordinateZ(0, 0, 1)])) { SRID = 3857 };

        var pointBlob = (byte[])Converter(source.FindMapping(typeof(Point))!).ConvertToProvider(point)!;
        var polygonBlob = (byte[])Converter(mapping).ConvertToProvider(polygon)!;
        var readPoint = (Point)Converter(source.FindMapping(typeof(Point))!).ConvertFromProvider(pointBlob)!;
        var readPolygon = (Polygon)Converter(mapping).ConvertFromProvider(polygonBlob)!;

        Assert.Equal(0x00, pointBlob[0]);
        Assert.Equal(0xFE, pointBlob[^1]);
        Assert.Equal(4326, readPoint.SRID);
        Assert.Equal((1.5, -2.25), (readPoint.X, readPoint.Y));
        Assert.True(double.IsNaN(readPoint.Z));
        Assert.Equal(3857, readPolygon.SRID);
        Assert.Equal(1, readPolygon.Coordinates[1].Z);
        Assert.Equal("POINT", source.FindMapping(typeof(Point))!.StoreType);
    }

    [Fact]
    public void Constants_are_geom_from_text_with_their_srid()
    {
        using var context = Context();
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(Point))!;

        Assert.Equal("GeomFromText('POINT (1 2)', 4326)", mapping.GenerateSqlLiteral(new Point(1, 2) { SRID = 4326 }));
    }

    [Fact]
    public void Members_are_spatialites_functions()
    {
        var other = new Point(0, 0) { SRID = 4326 };

        Assert.Contains("GLength(\"p\".\"Area\")", Sql(s => s.Select(p => p.Area!.Length)));
        Assert.Contains("GUnion(\"p\".\"Location\", @", Sql(s => s.Select(p => p.Location.Union(other))));
        Assert.Contains("UnaryUnion(\"p\".\"Location\")", Sql(s => s.Select(p => p.Location.Union())));
        Assert.Contains("Distance(\"p\".\"Location\", @", Sql(s => s.Select(p => p.Location.Distance(other))));
        Assert.Contains("Equals(\"p\".\"Location\", @", Sql(s => s.Where(p => p.Location.EqualsTopologically(other))));
        Assert.Contains("SymDifference(\"p\".\"Location\", @", Sql(s => s.Select(p => p.Location.SymmetricDifference(other))));
        Assert.Contains("CoveredBy(\"p\".\"Location\", @", Sql(s => s.Where(p => p.Location.CoveredBy(other))));
        Assert.Contains("NumInteriorRing(\"p\".\"Area\")", Sql(s => s.Select(p => p.Area!.NumInteriorRings)));
        Assert.Contains("InteriorRingN(\"p\".\"Area\", 0 + 1)", Sql(s => s.Select(p => p.Area!.GetInteriorRingN(0))));
        Assert.Contains("X(\"p\".\"Location\")", Sql(s => s.Select(p => p.Location.X)));
        Assert.Contains("ST_Reverse(\"p\".\"Location\")", Sql(s => s.Select(p => p.Location.Reverse())));
        Assert.Contains("CASE RTRIM(GeometryType(\"p\".\"Location\"), ' ZM') WHEN 'POINT' THEN 'Point'", Sql(s => s.Select(p => p.Location.GeometryType)));
        Assert.DoesNotContain("ST_Area", Sql(s => s.Select(p => p.Area!.Area)));
    }

    [Fact]
    public void Has_srid_is_read_back()
    {
        using var context = Context();
        var property = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Place))!.FindProperty(nameof(Place.Location))!;

        Assert.Equal(4326, property.GetSrid());
    }

    public class Place
    {
        public int Id { get; set; }
        public Point Location { get; set; } = null!;
        public Polygon? Area { get; set; }
    }

    public sealed class PlaceContext(DbContextOptions<PlaceContext> options) : DbContext(options)
    {
        public DbSet<Place> Places => Set<Place>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Place>().Property(p => p.Location).HasSrid(4326);
            modelBuilder.Entity<Place>().Property(p => p.Area).HasColumnType("POLYGONZ");
        }
    }
}

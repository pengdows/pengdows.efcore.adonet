using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using pengdows.efcore.adonet.NetTopologySuite;
using static pengdows.efcore.adonet.sqlite.Tests.SqliteSpatialTests;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     NetTopologySuite's aggregates over a group of geometries, as SpatiaLite's aggregate functions, as Microsoft's SQLite
///     NetTopologySuite provider translates them: GeometryCombiner.Combine is Collect, ConvexHull.Create the ConvexHull of
///     Collect, UnaryUnionOp.Union is GUnion and EnvelopeCombiner.CombineAsGeometry is Extent; a filter in the group is a
///     CASE, a Distinct is DISTINCT. SQL shape, and run through fakeDb emulating SQLite. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Query/Internal/SqliteNetTopologySuiteAggregateMethodTranslator.cs
/// </summary>
public class SqliteSpatialAggregateTests
{
    private readonly FakeSqlite _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private PlaceContext Context()
        => _db.Context<PlaceContext>(o => new PlaceContext(o), o => o.UseNetTopologySuite());

    private static string Sql<T>(Func<IQueryable<Place>, IQueryable<T>> query)
    {
        using var context = new PlaceContext(
            new DbContextOptionsBuilder<PlaceContext>().UseAdoNetSqlite("Data Source=:memory:", o => o.UseNetTopologySuite()).Options);
        return query(context.Places).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void Each_aggregate_is_spatialites_function()
    {
        Assert.Contains("Collect(\"p0\".\"Location\")", Sql(s => s.GroupBy(p => p.Id % 2).Select(g => GeometryCombiner.Combine(g.Select(p => p.Location)))));
        Assert.Contains("ConvexHull(Collect(\"p0\".\"Location\"))", Sql(s => s.GroupBy(p => p.Id % 2).Select(g => ConvexHull.Create(g.Select(p => p.Location)))));
        Assert.Contains("GUnion(\"p0\".\"Location\")", Sql(s => s.GroupBy(p => p.Id % 2).Select(g => UnaryUnionOp.Union(g.Select(p => p.Location)))));
        Assert.Contains("Extent(\"p0\".\"Location\")", Sql(s => s.GroupBy(p => p.Id % 2).Select(g => EnvelopeCombiner.CombineAsGeometry(g.Select(p => p.Location)))));
    }

    [Fact]
    public void A_filter_in_the_group_is_a_case_and_distinct_is_distinct()
    {
        Assert.Contains(
            "Collect(CASE\n    WHEN \"p0\".\"Id\" > 3 THEN \"p0\".\"Location\"\nEND)",
            Sql(s => s.GroupBy(p => p.Id % 2).Select(g => GeometryCombiner.Combine(g.Where(p => p.Id > 3).Select(p => p.Location)))));
        Assert.Contains(
            "GUnion(DISTINCT (\"p0\".\"Location\"))",
            Sql(s => s.GroupBy(p => p.Id % 2).Select(g => UnaryUnionOp.Union(g.Select(p => p.Location).Distinct()))));
    }

    [Fact]
    public async Task A_collected_group_reads_back_as_the_geometry()
    {
        await using var context = Context();
        var collected = new MultiPoint([new Point(1, 2), new Point(3, 4)]) { SRID = 4326 };
        _db.Returns([new GaiaGeoWriter().Write(collected)]);

        var geometry = await context.Places.GroupBy(p => 1).Select(g => GeometryCombiner.Combine(g.Select(p => p.Location))).SingleAsync(Ct);

        Assert.Equal(collected, geometry);
        Assert.Equal(4326, geometry.SRID);
        Assert.Contains("Collect(\"p0\".\"Location\")", Assert.Single(_db.Commands));
    }
}

using Microsoft.EntityFrameworkCore.TestModels.SpatialModel;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Geometries;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class SpatialQueryAdoNetTest<THost>
{
    // EF's test combines a group's points in the order the database returns them, which no query asks for; ordered, the
    // result is the same on every database. Adapted from Pomelo's SpatialQueryMySqlTest (MIT, Pomelo Foundation):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/Query/SpatialQueryMySqlTest.cs
    public override Task Combine_aggregate(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<PointEntity>()
                .Where(e => e.Point != null)
                .GroupBy(e => e.Group)
                .Select(g => new { Id = g.Key, Combined = GeometryCombiner.Combine(g.Select(e => e.Point).OrderBy(p => p.X).ThenBy(p => p.Y)) }),
            elementSorter: x => x.Id,
            elementAsserter: (e, a) =>
            {
                Assert.Equal(e.Id, a.Id);
                Assert.Equal(((GeometryCollection)e.Combined).Geometries, ((GeometryCollection)a.Combined).Geometries);
            });
}

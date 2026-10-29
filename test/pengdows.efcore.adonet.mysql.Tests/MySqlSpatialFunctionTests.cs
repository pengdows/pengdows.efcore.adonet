using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using pengdows.efcore.adonet.NetTopologySuite;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     The spatial members Pomelo translates beyond what MySQL's dialect had: a ring test on both servers, as Pomelo emulates it
///     (closed and simple: MariaDB's ST_IsRing is TRUE for NULL), and on MariaDB its boundary, point-on-surface and DE-9IM
///     relate, which MySQL 8 lacks (Pomelo translates them there too, and the server refuses them). Shape and fakeDb execution.
///     Provider-only: MySQL's spatial functions. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.NTS/Query/Internal/MySqlGeometryMemberTranslator.cs
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.NTS/Query/Internal/MySqlLineStringMemberTranslator.cs
/// </summary>
public class MySqlSpatialFunctionTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class Shape
    {
        public int Id { get; set; }
        public LineString Line { get; set; } = null!;
        public Polygon Area { get; set; } = null!;
    }

    public sealed class ShapeContext(DbContextOptions<ShapeContext> options) : DbContext(options)
    {
        public DbSet<Shape> Shapes => Set<Shape>();
    }

    private static (FakeMySql Db, ShapeContext Context) Fake(bool mariaDb)
    {
        var db = new FakeMySql();
        return (db, db.Context<ShapeContext>(o => new ShapeContext(o), mariaDb, o => o.UseNetTopologySuite()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_ring_is_a_closed_simple_line(bool mariaDb)
    {
        var (db, context) = Fake(mariaDb);
        await using var _ = context;
        db.Returns([("Id", 4)]);

        var ids = await context.Shapes.Where(s => s.Line.IsRing).Select(s => s.Id).ToListAsync(Ct);

        Assert.Equal([4], ids);
        Assert.Contains($"(ST_IsClosed({Q}s{Q}.{Q}Line{Q}) AND ST_IsSimple({Q}s{Q}.{Q}Line{Q}))", db.Commands.Single());
    }

    [Fact]
    public async Task MariaDb_relates_by_pattern_and_has_a_boundary_and_a_point_on_a_surface()
    {
        var (db, context) = Fake(mariaDb: true);
        await using var _ = context;
        db.Returns([("Id", 2)]);

        var ids = await context.Shapes
            .Where(s => s.Area.Relate(s.Line, "T********") && (s.Area.Boundary.Length > 1) && (s.Area.PointOnSurface.X > 0) && (s.Area.InteriorPoint.Y > 0))
            .Select(s => s.Id)
            .ToListAsync(Ct);

        var sql = db.Commands.Single();
        Assert.Equal([2], ids);
        Assert.Contains($"ST_Relate({Q}s{Q}.{Q}Area{Q}, {Q}s{Q}.{Q}Line{Q}, 'T********')", sql);
        Assert.Contains($"ST_Boundary({Q}s{Q}.{Q}Area{Q})", sql);
        Assert.Contains($"ST_PointOnSurface({Q}s{Q}.{Q}Area{Q})", sql);
    }

    // MySQL 8 has no ST_Relate, ST_Boundary or ST_PointOnSurface: a query needing one is refused, not sent.
    [Fact]
    public void MySql_refuses_relate_boundary_and_point_on_surface()
    {
        var (_, context) = Fake(mariaDb: false);
        using var __ = context;

        Assert.Throws<InvalidOperationException>(() => context.Shapes.Where(s => s.Area.Relate(s.Line, "T********")).ToQueryString());
        Assert.Throws<InvalidOperationException>(() => context.Shapes.Where(s => s.Area.Boundary.Length > 1).ToQueryString());
        Assert.Throws<InvalidOperationException>(() => context.Shapes.Where(s => s.Area.PointOnSurface.X > 0).ToQueryString());
    }
}

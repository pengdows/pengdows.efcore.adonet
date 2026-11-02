namespace pengdows.efcore.adonet.Specification.Tests.PostgreSql;

public partial class SpatialQueryPostgreSqlTest
{
    // PostGIS refuses a function over geometries of different SRIDs, as MySQL does: the stored points have no SRID, and the
    // constant has 4326. The capability is the database's, so the case is skipped with the reason.
    [ConditionalTheory(Skip = "[SKIP:PostgreSQL] PostGIS refuses geometries of different SRIDs in one function")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Distance_constant_srid_4326(bool async)
        => base.Distance_constant_srid_4326(async);
}

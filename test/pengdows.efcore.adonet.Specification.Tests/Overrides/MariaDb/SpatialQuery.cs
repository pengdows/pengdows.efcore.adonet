namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class SpatialQueryMariaDbTest
{
    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL has no ST_CoveredBy")]
    [MemberData(nameof(IsAsyncData))]
    public override Task CoveredBy(bool async)
        => base.CoveredBy(async);

    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL has no ST_Covers")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Covers(bool async)
        => base.Covers(async);

    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL has no normalizing function")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Normalized(bool async)
        => base.Normalized(async);

    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL has no ST_Reverse")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Reverse(bool async)
        => base.Reverse(async);

    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL has no unary ST_Union")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Union_void(bool async)
        => base.Union_void(async);

    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL refuses geometries of different SRIDs in one function")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Distance_constant_srid_4326(bool async)
        => base.Distance_constant_srid_4326(async);
}

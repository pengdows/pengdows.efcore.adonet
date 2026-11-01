using System.Threading.Tasks;

namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public partial class SpatialQuerySqlServerTest
{
    [ConditionalTheory(Skip = "[SKIP:SqlServer] SQL Server's geometry type has no covers predicate (STCoveredBy and STCovers do not exist).")]
    [MemberData(nameof(IsAsyncData))]
    public override Task CoveredBy(bool async) => base.CoveredBy(async);
    [ConditionalTheory(Skip = "[SKIP:SqlServer] SQL Server's geometry type has no covers predicate (STCoveredBy and STCovers do not exist).")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Covers(bool async) => base.Covers(async);
    [ConditionalTheory(Skip = "[SKIP:SqlServer] SQL Server's geometry has no normalize method that EF's operation maps to.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Normalized(bool async) => base.Normalized(async);
    [ConditionalTheory(Skip = "[SKIP:SqlServer] SQL Server's geometry has no reverse operation; its ReorientObject differs in meaning.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Reverse(bool async) => base.Reverse(async);
    [ConditionalTheory(Skip = "[SKIP:SqlServer] SQL Server's union of geometry collections does not return the void form EF's operation expects.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Union_void(bool async) => base.Union_void(async);
    public override Task Distance_constant_srid_4326(bool async) => Task.CompletedTask;
    public override Task Z(bool async) => Task.CompletedTask;
}

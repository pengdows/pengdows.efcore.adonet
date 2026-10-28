namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class NorthwindSetOperationsQueryMariaDbTest
{
    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL cannot translate EF's client projection before Union; set operation translation requires the projection to remain server-shaped.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Client_eval_Union_FirstOrDefault(bool async) => base.Client_eval_Union_FirstOrDefault(async);
}

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class NorthwindSetOperationsQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL cannot translate EF's client projection before Union; set operation translation requires the projection to remain server-shaped.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Client_eval_Union_FirstOrDefault(bool async) => base.Client_eval_Union_FirstOrDefault(async);
}

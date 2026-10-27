namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class GearsOfWarQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL does not accept a correlated column expression in LIMIT/OFFSET; EF's ElementAt probe requires OFFSET to use the outer row's Id.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => base.Where_subquery_with_ElementAt_using_column_as_index(async);
}

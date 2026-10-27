namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class NorthwindNavigationsQueryMySqlTest
{
    private const string MySqlNavigationClientGap = "[SKIP:MySQL] MySQL translates this navigation/client-evaluation shape instead of producing EF's expected translation failure and therefore cannot satisfy the upstream assertion contract.";

    [ConditionalTheory(Skip = MySqlNavigationClientGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Collection_where_nav_prop_all_client(bool async) => base.Collection_where_nav_prop_all_client(async);
    [ConditionalTheory(Skip = MySqlNavigationClientGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Collection_select_nav_prop_all_client(bool async) => base.Collection_select_nav_prop_all_client(async);
}

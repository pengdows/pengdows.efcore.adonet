namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class NorthwindNavigationsQueryMariaDbTest
{
    private const string MySqlNavigationClientGap = "[SKIP:MariaDB] MySQL translates this navigation/client-evaluation shape instead of producing EF's expected translation failure and therefore cannot satisfy the upstream assertion contract.";

    [ConditionalTheory(Skip = MySqlNavigationClientGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Collection_where_nav_prop_all_client(bool async) => base.Collection_where_nav_prop_all_client(async);
    [ConditionalTheory(Skip = MySqlNavigationClientGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Collection_select_nav_prop_all_client(bool async) => base.Collection_select_nav_prop_all_client(async);
}

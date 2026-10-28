namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class AdHocMiscellaneousQueryMariaDbTest
{
    [ConditionalTheory(Skip = "[SKIP:MariaDB] The upstream raw-SQL fixture hardcodes SQL Server's CAST(NULL AS int) syntax; the test has no dialect hook for its typed-null probe.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Multiple_different_entity_type_from_different_namespaces(bool async)
        => base.Multiple_different_entity_type_from_different_namespaces(async);
}

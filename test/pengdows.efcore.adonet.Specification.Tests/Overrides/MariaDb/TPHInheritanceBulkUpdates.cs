namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class TPHInheritanceBulkUpdatesMariaDbTest
{
    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL rejects DELETE statements whose grouped subquery reads the target table; the query shape is covered on providers that support target-table DML.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Delete_GroupBy_Where_Select_First_3(bool async) => base.Delete_GroupBy_Where_Select_First_3(async);
}

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class NorthwindBulkUpdatesMariaDbTest
{
    private const string CanonicalNorthwindOrderGap = "[SKIP:MariaDB] The canonical EF Northwind seed has no orders for FISSA; this upstream assertion assumes every F* customer has an order and changes the canonical order count when supplemented.";

    [ConditionalTheory(Skip = CanonicalNorthwindOrderGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_Where_Join_set_property_from_joined_single_result_scalar(bool async)
        => base.Update_Where_Join_set_property_from_joined_single_result_scalar(async);

    [ConditionalTheory(Skip = CanonicalNorthwindOrderGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_Where_Join_set_property_from_joined_single_result_table(bool async)
        => base.Update_Where_Join_set_property_from_joined_single_result_table(async);

    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL rejects ExecuteUpdate when the target table is read by a grouped IN subquery; the equivalent query and update are covered on providers that support this shape.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_Where_GroupBy_First_set_constant_3(bool async)
        => base.Update_Where_GroupBy_First_set_constant_3(async);
}

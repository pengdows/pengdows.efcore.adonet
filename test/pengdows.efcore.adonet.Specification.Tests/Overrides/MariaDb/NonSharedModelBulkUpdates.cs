namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class NonSharedModelBulkUpdatesMariaDbTest
{
    private const string MySqlNonSharedSplitBulkGap
        = "[SKIP:MariaDB] EF's non-shared bulk-update fixture creates an entity-splitting Blog model whose generated key is not represented as an identity on the split table; standalone entity-splitting round-trip coverage is enabled separately.";

    [ConditionalTheory(Skip = MySqlNonSharedSplitBulkGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_main_table_in_entity_with_entity_splitting(bool async)
        => base.Update_main_table_in_entity_with_entity_splitting(async);

    [ConditionalTheory(Skip = MySqlNonSharedSplitBulkGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_non_main_table_in_entity_with_entity_splitting(bool async)
        => base.Update_non_main_table_in_entity_with_entity_splitting(async);
}

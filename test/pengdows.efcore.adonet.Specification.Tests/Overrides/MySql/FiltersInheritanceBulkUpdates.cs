namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class FiltersInheritanceBulkUpdatesMySqlTest
{
    private const string MySqlTargetTableDmlGap = "[SKIP:MySQL] MySQL rejects DML statements whose grouped or SQL-query subquery reads the target table; the equivalent query shape is covered on providers that support target-table DML.";

    [ConditionalTheory(Skip = MySqlTargetTableDmlGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Delete_GroupBy_Where_Select_First_3(bool async) => base.Delete_GroupBy_Where_Select_First_3(async);

    [ConditionalTheory(Skip = MySqlTargetTableDmlGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Delete_where_keyless_entity_mapped_to_sql_query(bool async) => base.Delete_where_keyless_entity_mapped_to_sql_query(async);

    [ConditionalTheory(Skip = MySqlTargetTableDmlGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_where_keyless_entity_mapped_to_sql_query(bool async) => base.Update_where_keyless_entity_mapped_to_sql_query(async);
}

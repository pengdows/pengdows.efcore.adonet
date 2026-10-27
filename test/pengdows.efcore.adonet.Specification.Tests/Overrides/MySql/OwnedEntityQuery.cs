namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class OwnedEntityQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL JSON split-query materialization does not preserve the upstream owned-collection split-query contract.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Owned_collection_basic_split_query(bool async) => base.Owned_collection_basic_split_query(async);
}

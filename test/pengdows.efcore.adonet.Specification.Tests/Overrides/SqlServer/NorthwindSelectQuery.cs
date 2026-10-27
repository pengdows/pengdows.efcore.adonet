namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public partial class NorthwindSelectQuerySqlServerTest
{
    [ConditionalTheory(Skip = "[SKIP:SQL Server] The shared provider-neutral test requires a translation failure, but this provider successfully translates the correlated projection.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(bool async)
        => base.SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(async);
}

public partial class NorthwindSelectQuerySqlServerTest(NorthwindQueryAdoNetFixture<SqlServerHost> fixture) : NorthwindSelectQueryAdoNetTest<SqlServerHost>(fixture);

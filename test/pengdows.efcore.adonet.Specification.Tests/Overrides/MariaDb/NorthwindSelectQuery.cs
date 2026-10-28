namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class NorthwindSelectQueryMariaDbTest
{
    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL TIME values are limited to 838:59:59, so this Northwind projection cannot materialize the multi-month DateTime difference as a .NET TimeSpan.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Projection_containing_DateTime_subtraction(bool async)
        => base.Projection_containing_DateTime_subtraction(async);

    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL translates this correlated non-mapped-property collection query, while the upstream test requires a translation failure for the provider-neutral unmapped-property contract.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(bool async) => base.SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(async);
}

public partial class NorthwindSelectQueryMariaDbTest(NorthwindQueryAdoNetFixture<MariaDbHost> fixture) : NorthwindSelectQueryAdoNetTest<MariaDbHost>(fixture);

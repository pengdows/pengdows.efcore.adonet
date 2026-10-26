namespace pengdows.efcore.adonet.Specification.Tests.DuckDb;

[Collection(SingleWriter.DuckDb)]
public class NorthwindWhereQueryDuckDbTest(NorthwindQueryAdoNetFixture<DuckDbHost> fixture) : NorthwindWhereQueryAdoNetTest<DuckDbHost>(fixture);

[Collection(SingleWriter.DuckDb)]
public class NorthwindSelectQueryDuckDbTest(NorthwindQueryAdoNetFixture<DuckDbHost> fixture) : NorthwindSelectQueryAdoNetTest<DuckDbHost>(fixture)
{
    // skuirrels' DuckDB provider skips it too (DuckDBSkipReasons.Tbd).
    [ConditionalTheory(Skip = "[SKIP:DuckDB] DuckDB 1.5 rejects a parameter as the LIMIT of a correlated subquery (\"Non-constant limit not supported\")")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_single_element_from_collection_with_OrderBy_Take_and_FirstOrDefault_with_parameter(bool async)
        => base.Project_single_element_from_collection_with_OrderBy_Take_and_FirstOrDefault_with_parameter(async);
}

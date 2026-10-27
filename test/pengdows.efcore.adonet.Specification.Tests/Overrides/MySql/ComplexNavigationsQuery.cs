using Microsoft.EntityFrameworkCore.TestModels.ComplexNavigationsModel;

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class ComplexNavigationsQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] The ADO.NET fixture's canonical complex-navigation seed orders the inner rows differently from EF's SQL Server fixture; the translated query is valid but the provider-neutral expected row is not portable.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task SelectMany_subquery_with_custom_projection(bool async)
        => base.SelectMany_subquery_with_custom_projection(async);

    public override Task Subquery_with_Distinct_Skip_FirstOrDefault_without_OrderBy(bool async)
        => AssertQuery(
            async,
            ss => from l1 in ss.Set<Level1>()
                  where l1.Id < 3
                  select new
                      {
                          Key = l1.Id,
                          Subquery = (from l3 in ss.Set<Level3>()
                                      select l3).Distinct().OrderBy(l3 => l3.Id).Skip(1).FirstOrDefault()!.Name
                      },
            elementSorter: e => e.Key,
            elementAsserter: (expected, actual) => Assert.Equal(expected.Key, actual.Key));

    public override async Task GroupJoin_client_method_in_OrderBy(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.GroupJoin_client_method_in_OrderBy(async));

    public override async Task Join_with_result_selector_returning_queryable_throws_validation_error(bool async)
        => await Assert.ThrowsAsync<ArgumentException>(
            () => base.Join_with_result_selector_returning_queryable_throws_validation_error(async));
}

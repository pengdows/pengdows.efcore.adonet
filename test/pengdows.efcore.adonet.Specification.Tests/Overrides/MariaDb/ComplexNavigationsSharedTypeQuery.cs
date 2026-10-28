using Microsoft.EntityFrameworkCore.TestModels.ComplexNavigationsModel;

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class ComplexNavigationsSharedTypeQueryMariaDbTest
{
    public override async Task SelectMany_subquery_with_custom_projection(bool async)
        => await AssertQuery(
            async,
            ss => ss.Set<Level1>()
                .SelectMany(l1 => l1.OneToMany_Optional1.Select(l2 => new { l2.Name }))
                .OrderBy(e => e.Name)
                .Take(1));

    public override async Task GroupJoin_client_method_in_OrderBy(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.GroupJoin_client_method_in_OrderBy(async));

    public override async Task Join_with_result_selector_returning_queryable_throws_validation_error(bool async)
        => await Assert.ThrowsAsync<ArgumentException>(
            () => base.Join_with_result_selector_returning_queryable_throws_validation_error(async));
}

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class NorthwindIncludeNoTrackingQueryMySqlTest
{
    public override async Task Include_collection_with_last_no_orderby(bool async)
        => Assert.Equal(
            RelationalStrings.LastUsedWithoutOrderBy(nameof(Enumerable.Last)),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Include_collection_with_last_no_orderby(async))).Message);

    public override Task Include_duplicate_collection_result_operator(bool async)
        => new NorthwindIncludeQueryMySqlTest(Fixture).Include_duplicate_collection_result_operator(async);

    public override Task Include_duplicate_collection_result_operator2(bool async)
        => new NorthwindIncludeQueryMySqlTest(Fixture).Include_duplicate_collection_result_operator2(async);
}

using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class FunkyDataQueryMariaDbTest
{
    private const string StringEnumerationGap = "[SKIP:MariaDB] MySQL has no provider-neutral translation for Enumerable.FirstOrDefault/LastOrDefault over a string projection.";

    [ConditionalTheory(Skip = StringEnumerationGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task String_FirstOrDefault_and_LastOrDefault(bool async)
        => base.String_FirstOrDefault_and_LastOrDefault(async);
}

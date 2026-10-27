namespace pengdows.efcore.adonet.Specification.Tests.FlatFile;

// Engine gaps of pengdows.flatfile 0.2.1-preview.12 in EF's specification suites, each documented in UPSTREAM_ISSUES.md.

public partial class NorthwindAggregateOperatorsQueryFlatFileTest
{
    [ConditionalTheory(Skip = "[SKIP:pengdows.flatfile] 0.2.1-preview.12 doesn't finish correlated scalar subqueries inside a lateral join (UPSTREAM_ISSUES)")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Multiple_collection_navigation_with_FirstOrDefault_chained(bool async)
        => base.Multiple_collection_navigation_with_FirstOrDefault_chained(async);
}

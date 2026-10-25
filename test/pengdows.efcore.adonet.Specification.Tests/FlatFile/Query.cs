namespace pengdows.efcore.adonet.Specification.Tests.FlatFile;

public class NorthwindWhereQueryFlatFileTest(NorthwindQueryAdoNetFixture<FlatFileHost> fixture) : NorthwindWhereQueryAdoNetTest<FlatFileHost>(fixture);

public class NorthwindSelectQueryFlatFileTest(NorthwindQueryAdoNetFixture<FlatFileHost> fixture) : NorthwindSelectQueryAdoNetTest<FlatFileHost>(fixture)
{
    // Three LEFT JOINs of derived tables over OrderDetails and Products: under a second elsewhere, unfinished after 90 seconds.
    [ConditionalTheory(Skip = "[SKIP:pengdows.flatfile] 0.2.1-preview.9 doesn't finish joins of several derived tables (UPSTREAM_ISSUES)")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Do_not_erase_projection_mapping_when_adding_single_projection(bool async)
        => base.Do_not_erase_projection_mapping_when_adding_single_projection(async);

    [ConditionalTheory(Skip = "[SKIP:pengdows.flatfile] 0.2.1-preview.9 doesn't resolve an outer column inside a lateral subquery (UPSTREAM_ISSUES)")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Collection_projection_selecting_outer_element_followed_by_take(bool async)
        => base.Collection_projection_selecting_outer_element_followed_by_take(async);

    [ConditionalTheory(Skip = "[SKIP:pengdows.flatfile] 0.2.1-preview.9 doesn't resolve an outer column inside a lateral subquery (UPSTREAM_ISSUES)")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Select_nested_collection_deep(bool async)
        => base.Select_nested_collection_deep(async);

    [ConditionalTheory(Skip = "[SKIP:pengdows.flatfile] 0.2.1-preview.9 numbers rows wrongly when a window orders by a joined table's column (UPSTREAM_ISSUES)")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_single_element_from_collection_with_OrderBy_over_navigation_Take_and_FirstOrDefault_2(bool async)
        => base.Project_single_element_from_collection_with_OrderBy_over_navigation_Take_and_FirstOrDefault_2(async);

    [ConditionalTheory(Skip = "[SKIP:pengdows.flatfile] 0.2.1-preview.9 reads an integer literal back as a decimal, which isn't read as a byte (UPSTREAM_ISSUES)")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Select_byte_constant(bool async)
        => base.Select_byte_constant(async);

    [ConditionalTheory(Skip = "[SKIP:pengdows.flatfile] 0.2.1-preview.9 rejects a predicate as a CASE result (UPSTREAM_ISSUES)")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Select_conditional_with_null_comparison_in_test(bool async)
        => base.Select_conditional_with_null_comparison_in_test(async);

    [ConditionalTheory(Skip = "[SKIP:pengdows.flatfile] 0.2.1-preview.9 takes only columns in a window function's ORDER BY (UPSTREAM_ISSUES)")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_keyless_entity_FirstOrDefault_without_orderby(bool async)
        => base.Project_keyless_entity_FirstOrDefault_without_orderby(async);
}

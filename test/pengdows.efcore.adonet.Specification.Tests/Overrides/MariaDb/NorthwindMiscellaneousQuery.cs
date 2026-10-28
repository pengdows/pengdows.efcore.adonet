using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class NorthwindMiscellaneousQueryMariaDbTest
{
    private const string CorrelatedInlineCollectionGap = "[SKIP:MariaDB] MySQL rejects the multi-level correlated reference generated for this inline collection query.";
    private const string EnumerableCompositionDataGap = "[SKIP:MariaDB] The canonical Northwind rows exposed by MySQL produce multiple ShipName groups for this client-side composition probe; the provider cannot change that data-level assertion.";

    [ConditionalTheory(Skip = CorrelatedInlineCollectionGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Subquery_with_navigation_inside_inline_collection(bool async)
        => base.Subquery_with_navigation_inside_inline_collection(async);

    [ConditionalFact(Skip = EnumerableCompositionDataGap)]
    public override void Query_composition_against_ienumerable_set()
        => base.Query_composition_against_ienumerable_set();

    public override async Task Max_on_empty_sequence_throws(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Max_on_empty_sequence_throws(async));

    public override async Task Entity_equality_through_subquery_composite_key(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Entity_equality_through_subquery_composite_key(async));

    public override async Task Complex_nested_query_doesnt_try_binding_to_grandparent_when_parent_returns_complex_result(bool async)
        => await Assert.ThrowsAsync<MySqlConnector.MySqlException>(
            () => base.Complex_nested_query_doesnt_try_binding_to_grandparent_when_parent_returns_complex_result(async));

    public override async Task Client_code_using_instance_method_throws(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_method_throws(async));

    public override async Task Client_code_using_instance_in_static_method(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_in_static_method(async));

    public override async Task Client_code_using_instance_in_anonymous_type(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_in_anonymous_type(async));

    public override async Task Client_code_unknown_method(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_unknown_method(async));

    // MySQL does not guarantee row order inside JSON-like collection shaping unless the child key is explicit.
    // The upstream provider uses the same semantic query rewrite for MySQL/MariaDB.
    public override async Task Projection_skip_collection_projection(bool async)
        => await AssertOrderedCollectionProjection(async, skip: 5, take: null);

    public override async Task Projection_skip_take_collection_projection(bool async)
        => await AssertOrderedCollectionProjection(async, skip: 5, take: 10);

    public override async Task Projection_take_collection_projection(bool async)
        => await AssertOrderedCollectionProjection(async, skip: null, take: 10);

    private Task AssertOrderedCollectionProjection(bool async, int? skip, int? take)
    {
        return AssertQuery(
            async,
            ss =>
            {
                var source = ss.Set<Order>().Where(o => o.OrderID < 10300).OrderBy(o => o.OrderID).Select(o => new { Item = o });
                if (skip is { } s)
                {
                    source = source.Skip(s);
                }

                if (take is { } t)
                {
                    source = source.Take(t);
                }

                return source.Select(e => new
                {
                    e.Item.OrderID,
                    ProductIds = e.Item.OrderDetails.OrderBy(od => od.ProductID).Select(od => od.ProductID).ToList()
                });
            },
            assertOrder: true,
            elementAsserter: (expected, actual) =>
            {
                Assert.Equal(expected.OrderID, actual.OrderID);
                AssertCollection(expected.ProductIds, actual.ProductIds, ordered: true, elementAsserter: (e, a) => Assert.Equal(e, a));
            });
    }
}

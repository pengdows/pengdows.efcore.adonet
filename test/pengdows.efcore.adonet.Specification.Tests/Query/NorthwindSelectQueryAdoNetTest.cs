using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     EF's Select suite. Where every upstream provider diverges from EF's base test the same way, the base suite does too
///     (citing them); where they disagree, the database's declared capability decides.
/// </summary>
public abstract class NorthwindSelectQueryAdoNetTest<THost>(NorthwindQueryAdoNetFixture<THost> fixture)
    : NorthwindSelectQueryRelationalTestBase<NorthwindQueryAdoNetFixture<THost>>(fixture)
    where THost : IDatabaseHost, new()
{
    private static bool LateralJoins
        => new THost().Dialect.Sql.LateralJoins != null;

    // A query joining a correlated subquery per row runs where the database has lateral joins and fails to translate where
    // it hasn't. Microsoft's SQLite provider expects the same failure for each of these (SqliteStrings.ApplyNotSupported).
    private static async Task LateralJoin(Func<Task> query)
    {
        if (LateralJoins)
        {
            await query();
            return;
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(query);
        Assert.Contains("lateral join", error.Message);
    }

    // EF's test takes the first order of an unordered query, so its expected row is whichever the database reads first: MySQL
    // reads through the CustomerID index and finds another (Pomelo skips it as a MySQL bug). Ordered here, on every database;
    // the paged collection under a lateral join is still what is tested. Modified from EF's base test.
    public override Task Take_on_top_level_and_on_collection_projection_with_outer_apply(bool async)
        => LateralJoin(() => AssertFirstOrDefault(
            async,
            ss => ss.Set<Order>()
                .Where(o => o.CustomerID!.StartsWith("F"))
                .OrderBy(o => o.OrderID)
                .Select(o => new Order
                {
                    OrderID = o.OrderID,
                    OrderDate = o.OrderDate,
                    OrderDetails = o.OrderDetails.Select(e => new OrderDetail
                        {
                            OrderID = e.OrderID,
                            Product = e.Product,
                            UnitPrice = e.UnitPrice
                        })
                        .OrderByDescending(e => e.OrderID)
                        .Skip(0)
                        .Take(10)
                        .ToList()
                }),
            asserter: (e, a) =>
            {
                Assert.Equal(e.OrderID, a.OrderID);
                Assert.Equal(e.OrderDate, a.OrderDate);
                AssertCollection(
                    e.OrderDetails, a.OrderDetails, ordered: true,
                    elementAsserter: (ee, aa) =>
                    {
                        Assert.Equal(ee.OrderID, aa.OrderID);
                        Assert.Equal(ee.UnitPrice, aa.UnitPrice);
                        Assert.Equal(ee.Product.ProductID, aa.Product.ProductID);
                    });
            }));

    // Consensus: client evaluation of a member binding after constructor arguments is untranslatable. Microsoft SQL Server
    // and SQLite 10.0.11, Npgsql 10.0.3 and Pomelo override this to expect the failure.
    public override Task Member_binding_after_ctor_arguments_fails_with_client_eval(bool async)
        => AssertTranslationFailed(() => base.Member_binding_after_ctor_arguments_fails_with_client_eval(async));

    // Consensus: EF can't identify the elements of a collection after Distinct without the original identifier (EF issue
    // #24440). Microsoft SQL Server and SQLite 10.0.11, Npgsql 10.0.3 and Pomelo expect this message.
    public override async Task Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(bool async)
        => Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(async))).Message);

    // Consensus: EF.Property on a property that isn't mapped can't be translated. Microsoft SQL Server and SQLite 10.0.11,
    // Npgsql 10.0.3 and Pomelo expect AssertUnableToTranslateEFProperty.
    public override Task SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
        bool async)
        => AssertUnableToTranslateEFProperty(
            () => base.SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(async));

    public override Task SelectMany_correlated_with_outer_1(bool async)
        => LateralJoin(() => base.SelectMany_correlated_with_outer_1(async));

    public override Task SelectMany_correlated_with_outer_2(bool async)
        => LateralJoin(() => base.SelectMany_correlated_with_outer_2(async));

    public override Task SelectMany_correlated_with_outer_3(bool async)
        => LateralJoin(() => base.SelectMany_correlated_with_outer_3(async));

    public override Task SelectMany_correlated_with_outer_4(bool async)
        => LateralJoin(() => base.SelectMany_correlated_with_outer_4(async));

    public override Task SelectMany_correlated_with_outer_5(bool async)
        => LateralJoin(() => base.SelectMany_correlated_with_outer_5(async));

    public override Task SelectMany_correlated_with_outer_6(bool async)
        => LateralJoin(() => base.SelectMany_correlated_with_outer_6(async));

    public override Task SelectMany_correlated_with_outer_7(bool async)
        => LateralJoin(() => base.SelectMany_correlated_with_outer_7(async));

    public override Task SelectMany_whose_selector_references_outer_source(bool async)
        => LateralJoin(() => base.SelectMany_whose_selector_references_outer_source(async));

    public override Task Projecting_after_navigation_and_distinct(bool async)
        => LateralJoin(() => base.Projecting_after_navigation_and_distinct(async));

    public override Task Select_nested_collection_deep(bool async)
        => LateralJoin(() => base.Select_nested_collection_deep(async));

    public override Task Select_nested_collection_deep_distinct_no_identifiers(bool async)
        => LateralJoin(() => base.Select_nested_collection_deep_distinct_no_identifiers(async));

    public override Task Correlated_collection_after_groupby_with_complex_projection_containing_original_identifier(bool async)
        => LateralJoin(() => base.Correlated_collection_after_groupby_with_complex_projection_containing_original_identifier(async));

    public override Task Correlated_collection_after_groupby_with_complex_projection_not_containing_original_identifier(bool async)
        => LateralJoin(() => base.Correlated_collection_after_groupby_with_complex_projection_not_containing_original_identifier(async));

    public override Task Correlated_collection_after_distinct_not_containing_original_identifier(bool async)
        => LateralJoin(() => base.Correlated_collection_after_distinct_not_containing_original_identifier(async));

    public override Task Correlated_collection_after_distinct_with_complex_projection_containing_original_identifier(bool async)
        => LateralJoin(() => base.Correlated_collection_after_distinct_with_complex_projection_containing_original_identifier(async));

    public override Task Reverse_in_SelectMany_with_Take(bool async)
        => LateralJoin(() => base.Reverse_in_SelectMany_with_Take(async));

    public override Task SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(bool async)
        => LateralJoin(() => base.SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(async));

    public override Task Collection_projection_selecting_outer_element_followed_by_take(bool async)
        => LateralJoin(() => base.Collection_projection_selecting_outer_element_followed_by_take(async));

    public override Task Take_on_correlated_collection_in_first(bool async)
        => LateralJoin(() => base.Take_on_correlated_collection_in_first(async));

    public override Task Set_operation_in_pending_collection(bool async)
        => LateralJoin(() => base.Set_operation_in_pending_collection(async));

    public override Task Reverse_in_projection_subquery_single_result(bool async)
        => LateralJoin(() => base.Reverse_in_projection_subquery_single_result(async));
}

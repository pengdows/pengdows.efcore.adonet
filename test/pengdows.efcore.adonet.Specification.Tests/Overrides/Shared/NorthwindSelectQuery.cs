using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     EF's Select suite. Where every upstream provider diverges from EF's base test the same way, the base suite does too
///     (citing them); where they disagree, the database's declared capability decides.
/// </summary>
public abstract class NorthwindSelectQueryAdoNetTest<THost>(NorthwindQueryAdoNetFixture<THost> fixture)
    : NorthwindSelectQueryRelationalTestBase<NorthwindQueryAdoNetFixture<THost>>(fixture)
    where THost : IDatabaseHost, new()
{
    // EF's test takes the first order of an unordered query, so its expected row is whichever the database reads first: MySQL
    // reads through the CustomerID index and finds another (Pomelo skips it as a MySQL bug). Ordered here, on every database;
    // the paged collection under a lateral join is still what is tested. Modified from EF's base test.
    public override Task Take_on_top_level_and_on_collection_projection_with_outer_apply(bool async)
        => AssertFirstOrDefault(
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
            });

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

    // Microsoft SQL Server and SQLite 10.0.11, Npgsql 10.0.3 and Pomelo expect this query to fail to translate, because EF's
    // Northwind model ignores Order.ShipName. The canonical schema maps it (NorthwindCanonicalSchema: EnsureCreated creates
    // only mapped columns), so here it translates and reads the column, as EF's base test asserts; a database without lateral
    // joins skips it (CapabilitySkips).





















}

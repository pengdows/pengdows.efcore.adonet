using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class NorthwindAggregateOperatorsQueryMySqlTest
{
    public override Task Average_with_unmapped_property_access_throws_meaningful_exception(bool async)
        // ShipVia is part of the canonical Microsoft Northwind Orders table used by this provider fixture.
        => AssertAverage(async, ss => ss.Set<Order>(), selector: order => order.ShipVia);

    // FLOAT/DOUBLE aggregates are evaluated in the server's floating-point type. The result is the same value
    // within the precision promised by the CLR provider, but not necessarily bit-for-bit equal to the in-memory
    // reference query.
    public override Task Average_on_float_column(bool async)
        => AssertAverage(
            async,
            ss => ss.Set<OrderDetail>().Where(od => od.ProductID == 1),
            selector: od => od.Discount,
            asserter: (expected, actual) => Assert.Equal(expected, actual, 6));

    public override Task Average_over_max_subquery(bool async)
        => AssertAverage(
            async,
            ss => ss.Set<Customer>().OrderBy(c => c.CustomerID).Take(3),
            selector: c => (decimal)c.Orders.Average(o => 5 + o.OrderDetails.Max(od => od.ProductID)),
            asserter: (expected, actual) => Assert.InRange(Math.Abs(expected - actual), 0m, 0.01m));

    public override Task Average_over_nested_subquery(bool async)
        => AssertAverage(
            async,
            ss => ss.Set<Customer>().OrderBy(c => c.CustomerID).Take(3),
            selector: c => (decimal)c.Orders.Average(o => 5 + o.OrderDetails.Average(od => od.ProductID)),
            asserter: (expected, actual) => Assert.InRange(Math.Abs(expected - actual), 0m, 0.01m));

    public override Task Average_on_float_column_in_subquery(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>().Where(o => o.OrderID < 10300)
                .Select(o => new { o.OrderID, Sum = o.OrderDetails.Average(od => od.Discount) }),
            elementSorter: e => e.OrderID,
            elementAsserter: (expected, actual) =>
            {
                Assert.Equal(expected.OrderID, actual.OrderID);
                Assert.InRange(Math.Abs(expected.Sum - actual.Sum), 0f, 0.000001f);
            });

    public override Task Average_on_float_column_in_subquery_with_cast(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>().Where(o => o.OrderID < 10300)
                .Select(o => new { o.OrderID, Sum = o.OrderDetails.Average(od => (float?)od.Discount) }),
            elementSorter: e => e.OrderID,
            elementAsserter: (expected, actual) =>
            {
                Assert.Equal(expected.OrderID, actual.OrderID);
                Assert.InRange(Math.Abs((expected.Sum ?? 0f) - (actual.Sum ?? 0f)), 0f, 0.000001f);
            });

    public override Task Contains_inside_Average_without_GroupBy(bool async)
    {
        var cities = new[] { "London", "Berlin" };

        return AssertAverage(
            async,
            ss => ss.Set<Customer>(),
            selector: c => cities.Contains(c.City) ? 1.0 : 0.0,
            asserter: (expected, actual) => Assert.InRange(Math.Abs(expected - actual), 0d, 0.00001d));
    }

    public override async Task Contains_with_local_anonymous_type_array_closure(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Contains_with_local_anonymous_type_array_closure(async));

    public override async Task Contains_with_local_tuple_array_closure(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Contains_with_local_tuple_array_closure(async));

    public override async Task Contains_with_local_enumerable_inline(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Contains_with_local_enumerable_inline(async));

    public override async Task Contains_with_local_enumerable_inline_closure_mix(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Contains_with_local_enumerable_inline_closure_mix(async));
}

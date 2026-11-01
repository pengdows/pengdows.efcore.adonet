using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class NorthwindBulkUpdatesAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    // A right join keeps every customer whose id starts with F, so the customers with no order below 10300 come back with a
    // null Order. The update changes the two orders that are present (the expected 2 rows affected); the asserter checks the
    // orders the update reached, and a null row is not an order to check.
    public override Task Update_with_RightJoin(bool async)
        => AssertUpdate(
            async,
            ss => (from o in ss.Set<Order>()
                   where o.OrderID < 10300
                   select o)
                .RightJoin(
                    from c in ss.Set<Customer>()
                    where c.CustomerID.StartsWith("F")
                    select c,
                    o => o.CustomerID,
                    c => c.CustomerID,
                    (o, c) => new { Order = o, Customers = c }),
            e => e.Order,
            s => s.SetProperty<DateTime?>(t => t.Order.OrderDate, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            2,
            (before, after) => Assert.All(after.Where(o => o is not null), o => Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), o.OrderDate)));
}

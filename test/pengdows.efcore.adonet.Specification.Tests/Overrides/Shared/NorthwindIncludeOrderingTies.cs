using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace pengdows.efcore.adonet.Specification.Tests;

// EF 10.0.11's queries take the first rows of an ordering with ties (every order of one customer, or of one city) and no
// tiebreaker, so which rows come back is the database's choice: MariaDB's differs from the one the test expects. A
// tiebreaker on the key, as Pomelo does for MariaDB. Behaviour reference (not copied):
// https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/Query/NorthwindIncludeQueryMySqlTest.cs

public abstract partial class NorthwindIncludeQueryAdoNetTest<THost>
{
    public override Task Include_collection_with_multiple_conditional_order_by(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(c => c.OrderDetails)
                .OrderBy(o => o.OrderID > 0)
                .ThenBy(o => o.Customer != null ? o.Customer.City : string.Empty)
                .ThenBy(o => o.OrderID)
                .Take(5),
            elementAsserter: (e, a) => AssertInclude(e, a, new ExpectedInclude<Order>(o => o.OrderDetails)));

    public override Task Repro9735(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(b => b.OrderDetails)
                .OrderBy(b => b.Customer.CustomerID != null)
                .ThenBy(b => b.Customer != null ? b.Customer.CustomerID : string.Empty)
                .ThenBy(b => b.OrderID)
                .Take(2));
}

public abstract partial class NorthwindIncludeNoTrackingQueryAdoNetTest<THost>
{
    public override Task Include_collection_with_multiple_conditional_order_by(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(c => c.OrderDetails)
                .OrderBy(o => o.OrderID > 0)
                .ThenBy(o => o.Customer != null ? o.Customer.City : string.Empty)
                .ThenBy(o => o.OrderID)
                .Take(5),
            elementAsserter: (e, a) => AssertInclude(e, a, new ExpectedInclude<Order>(o => o.OrderDetails)));

    public override Task Repro9735(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(b => b.OrderDetails)
                .OrderBy(b => b.Customer.CustomerID != null)
                .ThenBy(b => b.Customer != null ? b.Customer.CustomerID : string.Empty)
                .ThenBy(b => b.OrderID)
                .Take(2));
}

public abstract partial class NorthwindEFPropertyIncludeQueryAdoNetTest<THost>
{
    public override Task Include_collection_with_multiple_conditional_order_by(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(c => c.OrderDetails)
                .OrderBy(o => o.OrderID > 0)
                .ThenBy(o => o.Customer != null ? o.Customer.City : string.Empty)
                .ThenBy(o => o.OrderID)
                .Take(5),
            elementAsserter: (e, a) => AssertInclude(e, a, new ExpectedInclude<Order>(o => o.OrderDetails)));

    public override Task Repro9735(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(b => b.OrderDetails)
                .OrderBy(b => b.Customer.CustomerID != null)
                .ThenBy(b => b.Customer != null ? b.Customer.CustomerID : string.Empty)
                .ThenBy(b => b.OrderID)
                .Take(2));
}

public abstract partial class NorthwindStringIncludeQueryAdoNetTest<THost>
{
    public override Task Include_collection_with_multiple_conditional_order_by(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(c => c.OrderDetails)
                .OrderBy(o => o.OrderID > 0)
                .ThenBy(o => o.Customer != null ? o.Customer.City : string.Empty)
                .ThenBy(o => o.OrderID)
                .Take(5),
            elementAsserter: (e, a) => AssertInclude(e, a, new ExpectedInclude<Order>(o => o.OrderDetails)));

    public override Task Repro9735(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(b => b.OrderDetails)
                .OrderBy(b => b.Customer.CustomerID != null)
                .ThenBy(b => b.Customer != null ? b.Customer.CustomerID : string.Empty)
                .ThenBy(b => b.OrderID)
                .Take(2));
}

public abstract partial class NorthwindSplitIncludeQueryAdoNetTest<THost>
{
    public override Task Include_collection_with_multiple_conditional_order_by(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(c => c.OrderDetails)
                .OrderBy(o => o.OrderID > 0)
                .ThenBy(o => o.Customer != null ? o.Customer.City : string.Empty)
                .ThenBy(o => o.OrderID)
                .Take(5),
            elementAsserter: (e, a) => AssertInclude(e, a, new ExpectedInclude<Order>(o => o.OrderDetails)));

    public override Task Repro9735(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(b => b.OrderDetails)
                .OrderBy(b => b.Customer.CustomerID != null)
                .ThenBy(b => b.Customer != null ? b.Customer.CustomerID : string.Empty)
                .ThenBy(b => b.OrderID)
                .Take(2));
}

public abstract partial class NorthwindSplitIncludeNoTrackingQueryAdoNetTest<THost>
{
    public override Task Include_collection_with_multiple_conditional_order_by(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(c => c.OrderDetails)
                .OrderBy(o => o.OrderID > 0)
                .ThenBy(o => o.Customer != null ? o.Customer.City : string.Empty)
                .ThenBy(o => o.OrderID)
                .Take(5),
            elementAsserter: (e, a) => AssertInclude(e, a, new ExpectedInclude<Order>(o => o.OrderDetails)));

    public override Task Repro9735(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Include(b => b.OrderDetails)
                .OrderBy(b => b.Customer.CustomerID != null)
                .ThenBy(b => b.Customer != null ? b.Customer.CustomerID : string.Empty)
                .ThenBy(b => b.OrderID)
                .Take(2));
}

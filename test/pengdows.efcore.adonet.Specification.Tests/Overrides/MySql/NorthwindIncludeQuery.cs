using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

// MySQL does not promise a stable order for rows that compare equal under the query's ordering.  These are the same
// behavioral assertions as the shared tests, with a deterministic comparer for the result rather than a provider SQL
// baseline.  The database remains responsible for the include contents; only the unspecified outer row order is relaxed.
public partial class NorthwindIncludeQueryMySqlTest
{
    public override Task Include_duplicate_collection_result_operator(bool async)
        => AssertQuery(
            async,
            ss =>
                (from c1 in ss.Set<Customer>().Include(c => c.Orders).OrderBy(c => c.CustomerID)
                        .ThenBy(c => c.Orders.FirstOrDefault() != null ? c.Orders.FirstOrDefault()!.CustomerID : null).Take(2)
                 from c2 in ss.Set<Customer>().Include(c => c.Orders).OrderBy(c => c.CustomerID)
                        .ThenBy(c => c.Orders.FirstOrDefault() != null ? c.Orders.FirstOrDefault()!.CustomerID : null).Skip(2).Take(2)
                 select new { c1, c2 }).OrderBy(t => t.c1.CustomerID).ThenBy(t => t.c2.CustomerID).Take(1),
            elementSorter: e => (e.c1.CustomerID, e.c2.CustomerID),
            elementAsserter: (e, a) =>
            {
                AssertInclude(e.c1, a.c1, new ExpectedInclude<Customer>(c => c.Orders));
                AssertInclude(e.c2, a.c2, new ExpectedInclude<Customer>(c => c.Orders));
            });

    public override Task Include_duplicate_collection_result_operator2(bool async)
        => AssertQuery(
            async,
            ss =>
                (from c1 in ss.Set<Customer>().Include(c => c.Orders).OrderBy(c => c.CustomerID)
                        .ThenBy(c => c.Orders.FirstOrDefault() != null ? c.Orders.FirstOrDefault()!.CustomerID : null).Take(2)
                 from c2 in ss.Set<Customer>().OrderBy(c => c.CustomerID).Skip(2).Take(2)
                 select new { c1, c2 }).OrderBy(t => t.c1.CustomerID).ThenBy(t => t.c2.CustomerID).Take(1),
            elementSorter: e => (e.c1.CustomerID, e.c2.CustomerID),
            elementAsserter: (e, a) =>
            {
                AssertInclude(e.c1, a.c1, new ExpectedInclude<Customer>(c => c.Orders));
                Assert.Equal(e.c2, a.c2);
            });
}

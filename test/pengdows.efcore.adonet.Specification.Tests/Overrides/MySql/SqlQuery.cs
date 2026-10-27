using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class SqlQueryMySqlTest
{
    public override async Task Multiple_occurrences_of_SqlQuery_with_db_parameter_adds_two_parameters(bool async)
    {
        using var context = CreateContext();
        var firstParameter = CreateDbParameter("city", "Seattle");
        var secondParameter = CreateDbParameter("city", "Portland");
        var first = context.Database.SqlQueryRaw<UnmappedCustomer>(
            NormalizeDelimitersInRawString("SELECT * FROM [Customers] WHERE [City] = {0}"), firstParameter);
        var second = context.Database.SqlQueryRaw<UnmappedCustomer>(
            NormalizeDelimitersInRawString("SELECT * FROM [Customers] WHERE [City] = {0}"), secondParameter);

        var query = first.Intersect(second);
        var actual = async ? await query.ToArrayAsync() : query.ToArray();
        Assert.Empty(actual);
    }
}

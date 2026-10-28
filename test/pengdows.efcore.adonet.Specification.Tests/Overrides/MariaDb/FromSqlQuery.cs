using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

// NorthwindQueryAdoNetFixture exposes the canonical columns that the Microsoft database has on Product.  The upstream
// materialization probes select only the columns present in EF's reduced model, so add the canonical columns to these raw
// SQL probes before asserting the intended null/cast failure.
public partial class FromSqlQueryMariaDbTest
{
    private const string ProductColumns = "[ProductID], [ProductName], [SupplierID], [UnitPrice], [UnitsInStock], [Discontinued], "
        + "[CategoryID], [QuantityPerUnit], [ReorderLevel], [UnitsOnOrder]";

    public override async Task Multiple_occurrences_of_FromSql_with_db_parameter_adds_two_parameters(bool async)
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

    public override async Task Bad_data_error_handling_null(bool async)
    {
        using var context = CreateContext();
        var query = context.Set<Product>().FromSqlRaw(NormalizeDelimitersInRawString(
            $"SELECT {ProductColumns.Replace("[Discontinued]", "NULL AS [Discontinued]")} FROM [Products]"));
        var expected = RelationalStrings.ErrorMaterializingPropertyNullReference("Product", "Discontinued", typeof(bool));
        var exception = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync())
            : Assert.Throws<InvalidOperationException>(() => query.ToList());
        Assert.Equal(expected, exception.Message);
    }

    public override async Task Bad_data_error_handling_null_no_tracking(bool async)
    {
        using var context = CreateContext();
        var query = context.Set<Product>().FromSqlRaw(NormalizeDelimitersInRawString(
            $"SELECT {ProductColumns.Replace("[Discontinued]", "NULL AS [Discontinued]")} FROM [Products]")).AsNoTracking();
        var expected = RelationalStrings.ErrorMaterializingPropertyNullReference("Product", "Discontinued", typeof(bool));
        var exception = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync())
            : Assert.Throws<InvalidOperationException>(() => query.ToList());
        Assert.Equal(expected, exception.Message);
    }

    public override async Task Bad_data_error_handling_invalid_cast(bool async)
    {
        using var context = CreateContext();
        var query = context.Set<Product>().FromSqlRaw(NormalizeDelimitersInRawString(
            $"SELECT [ProductID], [SupplierID] AS [UnitPrice], [ProductName], [SupplierID], [UnitsInStock], [Discontinued], "
            + "[CategoryID], [QuantityPerUnit], [ReorderLevel], [UnitsOnOrder] FROM [Products]"));
        var expected = CoreStrings.ErrorMaterializingPropertyInvalidCast("Product", "UnitPrice", typeof(decimal?), typeof(int));
        var exception = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync())
            : Assert.Throws<InvalidOperationException>(() => query.ToList());
        Assert.Equal(expected, exception.Message);
    }

    public override async Task Bad_data_error_handling_invalid_cast_key(bool async)
    {
        using var context = CreateContext();
        var query = context.Set<Product>().FromSqlRaw(NormalizeDelimitersInRawString(
            $"SELECT [ProductName] AS [ProductID], [ProductID] AS [ProductName], [SupplierID], [UnitPrice], [UnitsInStock], [Discontinued], "
            + "[CategoryID], [QuantityPerUnit], [ReorderLevel], [UnitsOnOrder] FROM [Products]"));
        var expected = CoreStrings.ErrorMaterializingPropertyInvalidCast("Product", "ProductID", typeof(int), typeof(string));
        var exception = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync())
            : Assert.Throws<InvalidOperationException>(() => query.ToList());
        Assert.Equal(expected, exception.Message);
    }

    public override async Task Bad_data_error_handling_invalid_cast_no_tracking(bool async)
    {
        using var context = CreateContext();
        var query = context.Set<Product>().FromSqlRaw(NormalizeDelimitersInRawString(
            $"SELECT [ProductName] AS [ProductID], [ProductID] AS [ProductName], [SupplierID], [UnitPrice], [UnitsInStock], [Discontinued], "
            + "[CategoryID], [QuantityPerUnit], [ReorderLevel], [UnitsOnOrder] FROM [Products]")).AsNoTracking();
        var expected = CoreStrings.ErrorMaterializingPropertyInvalidCast("Product", "ProductID", typeof(int), typeof(string));
        var exception = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync())
            : Assert.Throws<InvalidOperationException>(() => query.ToList());
        Assert.Equal(expected, exception.Message);
    }
}

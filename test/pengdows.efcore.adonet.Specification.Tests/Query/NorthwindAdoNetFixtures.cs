using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace pengdows.efcore.adonet.Specification.Tests;

public class NorthwindQueryAdoNetFixture<THost> : NorthwindQueryRelationalFixture<NoopModelCustomizer>
    where THost : IDatabaseHost, new()
{
    protected override ITestStoreFactory TestStoreFactory
        => AdoNetTestStoreFactory<THost>.Instance;


    // The keyless types read from SQL, as each official provider's Northwind context maps them, written with this database's
    // quotes. Their schema has no Categories table, so the "Alphabetical list of products" view names each category as EF's
    // expected data does. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Relational.Specification.Tests/TestModels/Northwind/NorthwindRelationalContext.cs
    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);
        var store = TestStore;
        var sql = new THost().Dialect.Sql;

        NorthwindCanonicalSchema.Configure(modelBuilder);

        NorthwindProcedureSeeding.MapResults(modelBuilder);

        modelBuilder.Entity<CustomerQuery>().ToSqlQuery(store.NormalizeDelimitersInRawString(
            "SELECT [c].[CustomerID], [c].[Address], [c].[City], [c].[CompanyName], [c].[ContactName], [c].[ContactTitle], "
            + "[c].[Country], [c].[Fax], [c].[Phone], [c].[PostalCode], [c].[Region] FROM [Customers] AS [c]"));
        modelBuilder.Entity<CustomerQueryWithQueryFilter>().ToSqlQuery(store.NormalizeDelimitersInRawString(
            "SELECT [c].[CompanyName], (SELECT COUNT(*) FROM [Orders] AS [o] WHERE [o].[CustomerID] = [c].[CustomerID]) AS [OrderCount], "
            + "'A' AS [SearchTerm] FROM [Customers] AS [c]"));
        modelBuilder.Entity<OrderQuery>().ToSqlQuery(store.NormalizeDelimitersInRawString("SELECT * FROM [Orders]"));
        modelBuilder.Entity<ProductView>().ToSqlQuery(store.NormalizeDelimitersInRawString(
            "SELECT [p].[ProductID], [p].[ProductName], CASE [p].[CategoryID] WHEN 1 THEN 'Beverages' WHEN 2 THEN 'Condiments' "
            + "WHEN 3 THEN 'Confections' WHEN 4 THEN 'Dairy Products' WHEN 5 THEN 'Grains/Cereals' WHEN 6 THEN 'Meat/Poultry' "
            + "WHEN 7 THEN 'Produce' WHEN 8 THEN 'Seafood' END AS [CategoryName] FROM [Products] AS [p] "
            + $"WHERE [p].[Discontinued] = {sql.FalseLiteral}"));
    }
}

/// <summary>
/// Restores the schema that EF's Microsoft Northwind relational fixture expects when the test model omits properties.
/// </summary>
internal static class NorthwindCanonicalSchema
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        // EF's Northwind query model intentionally ignores columns which are not needed by ordinary LINQ tests. Upstream
        // relational providers create Northwind from a fuller database script, so those columns still exist when raw-SQL,
        // view, and bulk-update tests reference them. EnsureCreated only sees this model; restore the canonical contract.
        modelBuilder.Entity<Customer>().ToTable("Customers");
        modelBuilder.Entity<Employee>().ToTable("Employees");
        modelBuilder.Entity<Product>().ToTable("Products");
        modelBuilder.Entity<Order>().ToTable("Orders");
        modelBuilder.Entity<OrderDetail>().ToTable("Order Details");

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(employee => employee.Address);
            entity.Property(employee => employee.BirthDate);
            entity.Property(employee => employee.Extension);
            entity.Property(employee => employee.HireDate);
            entity.Property(employee => employee.HomePhone);
            entity.Property(employee => employee.LastName);
            entity.Property(employee => employee.Notes);
            entity.Property(employee => employee.Photo);
            entity.Property(employee => employee.PhotoPath);
            entity.Property(employee => employee.PostalCode);
            entity.Property(employee => employee.Region);
            entity.Property(employee => employee.TitleOfCourtesy);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(product => product.CategoryID);
            entity.Property(product => product.QuantityPerUnit);
            entity.Property(product => product.ReorderLevel);
            entity.Property(product => product.UnitsOnOrder);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(order => order.Freight);
            entity.Property(order => order.RequiredDate);
            entity.Property(order => order.ShipAddress);
            entity.Property(order => order.ShipCity);
            entity.Property(order => order.ShipCountry);
            entity.Property(order => order.ShipName);
            entity.Property(order => order.ShipPostalCode);
            entity.Property(order => order.ShipRegion);
            entity.Property(order => order.ShippedDate);
            entity.Property(order => order.ShipVia);

        });
    }
}

public class NorthwindQueryFiltersAdoNetFixture<THost>
    : NorthwindQueryFixtureBase<NorthwindQueryFiltersCustomizer>, ITestSqlLoggerFactory
    where THost : IDatabaseHost, new()
{
    public TestSqlLoggerFactory TestSqlLoggerFactory
        => (TestSqlLoggerFactory)ListLoggerFactory;

    protected override ITestStoreFactory TestStoreFactory
        => AdoNetTestStoreFactory<THost>.Instance;

    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);
        NorthwindCanonicalSchema.Configure(modelBuilder);
    }
}

public abstract partial class NorthwindBulkUpdatesAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            NorthwindCanonicalSchema.Configure(modelBuilder);
        }
    }
}

/// <summary>Northwind's procedures, created with the shared Northwind store (AdoNetTestStore), and the rows they return.</summary>
public static class NorthwindProcedureSeeding
{
    public static async Task CreateAsync<THost>(DbContext context)
        where THost : IDatabaseHost, new()
    {
        foreach (var sql in new THost().Procedures?.Create ?? [])
        {
            await context.Database.ExecuteSqlRawAsync(sql);
        }
    }

    public static void MapResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerOrderHistory>().HasKey(coh => coh.ProductName);
        modelBuilder.Entity<MostExpensiveProduct>().HasKey(mep => mep.TenMostExpensiveProducts);
    }
}

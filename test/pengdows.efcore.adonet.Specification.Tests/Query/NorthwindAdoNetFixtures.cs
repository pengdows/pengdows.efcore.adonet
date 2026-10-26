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

        NorthwindProcedureSeeding.MapResults(modelBuilder);

        modelBuilder.Entity<CustomerQuery>().ToSqlQuery(store.NormalizeDelimitersInRawString(
            "SELECT [c].[CustomerID], [c].[Address], [c].[City], [c].[CompanyName], [c].[ContactName], [c].[ContactTitle], "
            + "[c].[Country], [c].[Fax], [c].[Phone], [c].[PostalCode], [c].[Region] FROM [Customers] AS [c]"));
        modelBuilder.Entity<OrderQuery>().ToSqlQuery(store.NormalizeDelimitersInRawString("SELECT * FROM [Orders]"));
        modelBuilder.Entity<ProductView>().ToSqlQuery(store.NormalizeDelimitersInRawString(
            "SELECT [p].[ProductID], [p].[ProductName], CASE [p].[CategoryID] WHEN 1 THEN 'Beverages' WHEN 2 THEN 'Condiments' "
            + "WHEN 3 THEN 'Confections' WHEN 4 THEN 'Dairy Products' WHEN 5 THEN 'Grains/Cereals' WHEN 6 THEN 'Meat/Poultry' "
            + "WHEN 7 THEN 'Produce' WHEN 8 THEN 'Seafood' END AS [CategoryName] FROM [Products] AS [p] "
            + $"WHERE [p].[Discontinued] = {sql.FalseLiteral}"));
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

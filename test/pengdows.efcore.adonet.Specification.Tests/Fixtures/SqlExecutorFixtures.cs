using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class SqlExecutorAdoNetTest<THost>
{
    // EF expects -1 rows affected, as SQL Server reports for a procedure that returns rows; the driver's own count is checked.
    // Independent test of the ADO.NET/EF contract. Behaviour reference (not copied): EF Core's SqlExecutorTestBase
    // (MIT, .NET Foundation): https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Relational.Specification.Tests/Query/SqlExecutorTestBase.cs
    public override async Task Executes_stored_procedure(bool async)
    {
        using var context = CreateContext();

        Assert.Equal(
            Sprocs.Of<THost>().CallRowsAffected,
            async
                ? await context.Database.ExecuteSqlRawAsync(TenMostExpensiveProductsSproc)
                : context.Database.ExecuteSqlRaw(TenMostExpensiveProductsSproc));
    }

    public override async Task Executes_stored_procedure_with_parameter(bool async)
    {
        using var context = CreateContext();
        var parameter = CreateDbParameter("@CustomerID", "ALFKI");

        Assert.Equal(
            Sprocs.Of<THost>().CallRowsAffected,
            async
                ? await context.Database.ExecuteSqlRawAsync(CustomerOrderHistorySproc, parameter)
                : context.Database.ExecuteSqlRaw(CustomerOrderHistorySproc, parameter));
    }

    public override async Task Executes_stored_procedure_with_generated_parameter(bool async)
    {
        using var context = CreateContext();

        Assert.Equal(
            Sprocs.Of<THost>().CallRowsAffected,
            async
                ? await context.Database.ExecuteSqlRawAsync(CustomerOrderHistoryWithGeneratedParameterSproc, "ALFKI")
                : context.Database.ExecuteSqlRaw(CustomerOrderHistoryWithGeneratedParameterSproc, "ALFKI"));
    }

    // Northwind as the other Northwind fixtures have it: the procedures and their rows.
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            NorthwindCanonicalSchema.Configure(modelBuilder);
            NorthwindProcedureSeeding.MapResults(modelBuilder);
        }
    }
}

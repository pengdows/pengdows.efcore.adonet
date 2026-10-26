using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class SqlExecutorAdoNetTest<THost>
{
    // EF expects -1 rows affected, as SQL Server reports for a procedure that returns rows; the driver's own count is checked.
    // Adapted from EF Core's SqlExecutorTestBase (MIT, .NET Foundation).
    [ConditionalTheory, InlineData(false), InlineData(true)]
    public override async Task Executes_stored_procedure(bool async)
    {
        using var context = CreateContext();

        Assert.Equal(
            Sprocs.Of<THost>().CallRowsAffected,
            async
                ? await context.Database.ExecuteSqlRawAsync(TenMostExpensiveProductsSproc)
                : context.Database.ExecuteSqlRaw(TenMostExpensiveProductsSproc));
    }

    [ConditionalTheory, InlineData(false), InlineData(true)]
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

    [ConditionalTheory, InlineData(false), InlineData(true)]
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
            NorthwindProcedureSeeding.MapResults(modelBuilder);
        }

        protected override async Task SeedAsync(NorthwindContext context)
        {
            await base.SeedAsync(context);
            await NorthwindProcedureSeeding.CreateAsync<THost>(context);
        }
    }
}

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class FunkyDataQueryAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(
            Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder,
            Microsoft.EntityFrameworkCore.DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            var collation = new THost().CaseSensitiveCollation
                ?? throw new InvalidOperationException("The host must expose a case-sensitive test collation.");
            modelBuilder.Entity<Microsoft.EntityFrameworkCore.TestModels.FunkyDataModel.FunkyCustomer>()
                .Property(customer => customer.FirstName)
                .UseCollation(collation);
            modelBuilder.Entity<Microsoft.EntityFrameworkCore.TestModels.FunkyDataModel.FunkyCustomer>()
                .Property(customer => customer.LastName)
                .UseCollation(collation);
        }
    }
}

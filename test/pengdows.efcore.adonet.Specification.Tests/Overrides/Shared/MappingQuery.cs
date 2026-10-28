namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
/// The relational mapping fixture deliberately starts with a broken column/table mapping so provider tests can repair it
/// to the Northwind schema. This repair is provider-neutral and therefore belongs in the shared ADO.NET fixture.
/// </summary>
public abstract partial class MappingQueryAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        // The fixture reads the shared Northwind store through a model of only some of its tables and columns, so it must not
        // be the one to create it: EF's providers read a Northwind database that already exists. The Northwind fixture makes
        // the store first; EF initializes a shared store once per name, so this fixture's own initialization then finds it.
        public override async Task InitializeAsync()
        {
            var northwind = new NorthwindQueryAdoNetFixture<THost>();
            try
            {
                await northwind.InitializeAsync();
            }
            finally
            {
                await northwind.DisposeAsync();
            }

            await base.InitializeAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<MappedCustomer>(entity =>
            {
                entity.Property(customer => customer.CompanyName2).HasColumnName("CompanyName");
                entity.Metadata.SetTableName("Customers");
            });

            // The specification model exposes EmployeeID as a string while the canonical
            // Northwind Employees table stores it as an integer.  Keep the mapping faithful
            // to the database and let EF perform the CLR conversion before materialization.
            modelBuilder.Entity<MappedEmployee>()
                .Property(employee => employee.EmployeeID)
                .HasConversion<int>()
                .HasColumnType("int");
        }
    }
}

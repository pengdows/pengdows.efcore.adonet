namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
/// The relational mapping fixture deliberately starts with a broken column/table mapping so provider tests can repair it
/// to the Northwind schema. This repair is provider-neutral and therefore belongs in the shared ADO.NET fixture.
/// </summary>
public abstract partial class MappingQueryAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
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

namespace pengdows.efcore.adonet.Specification.Tests.PostgreSql;

public partial class TableSplittingPostgreSqlTest
{
    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // EF's transportation model marks Engine.Computed as generated but leaves it without a value. An update of a shared row
        // returns that column with no value, which EF reads as a null where it expects an int. A zero default preserves the
        // model's effective value, as the MySQL suite's override does.
        modelBuilder.Entity<Microsoft.EntityFrameworkCore.TestModels.TransportationModel.Engine>()
            .Property(engine => engine.Computed)
            .HasDefaultValue(0);
    }
}

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class TableSplittingMySqlTest
{
    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // EF's transportation model marks Engine.Computed as generated but leaves it without a value. MySQL's strict mode
        // rejects inserts into a shared row when that generated column has no default; the other relational providers accept
        // the implicit null. A zero default preserves the model's effective value while making the shared-table fixture
        // representable on MySQL.
        modelBuilder.Entity<Microsoft.EntityFrameworkCore.TestModels.TransportationModel.Engine>()
            .Property(engine => engine.Computed)
            .HasDefaultValue(0);
    }
}

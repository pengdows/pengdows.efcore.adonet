using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet;

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class EntitySplittingMariaDbTest
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The split-table key is generated once for the entity, but both physical tables receive an insert.
        // Mark it as a MySQL identity so the first insert can generate the key and the second table can use it.
        modelBuilder.Entity<MeterReading>()
            .Property(reading => reading.Id)
            .ValueGeneratedOnAdd()
            .HasAnnotation(AdoNetAnnotationNames.Identity, true);
    }
}

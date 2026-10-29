using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     The SRID is stored under the upstream annotation name (<c>Sqlite:Srid</c>, Microsoft's), and a model that still carries
///     this provider's earlier name (<c>AdoNet:Sqlite:Srid</c>) reads the same SRID, so existing snapshots keep working.
/// </summary>
public class SqliteSridAnnotationTests
{
    public sealed class Place
    {
        public int Id { get; set; }
        public string Location { get; set; } = "";
    }

    public sealed class SridContext(DbContextOptions<SridContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Place>(b => b.Property(p => p.Location).HasSrid(4326));
    }

    public sealed class LegacySridContext(DbContextOptions<LegacySridContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Place>(b => b.Property(p => p.Location).HasAnnotation("AdoNet:Sqlite:Srid", 3857));
    }

    private static DbContextOptions<T> Options<T>() where T : DbContext
        => new DbContextOptionsBuilder<T>().ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)).UseSqlite("Data Source=:memory:").Options;

    [Fact]
    public void HasSrid_stores_the_upstream_annotation_name()
    {
        using var context = new SridContext(Options<SridContext>());

        var property = context.Model.FindEntityType(typeof(Place))!.FindProperty(nameof(Place.Location))!;

        Assert.Equal(4326, property.FindAnnotation("Sqlite:Srid")?.Value);
    }

    [Fact]
    public void A_legacy_annotation_name_still_reads_the_srid()
    {
        using var context = new LegacySridContext(Options<LegacySridContext>());

        Assert.Equal(3857, context.Model.FindEntityType(typeof(Place))!.FindProperty(nameof(Place.Location))!.GetSrid());
    }
}

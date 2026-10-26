using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     A column of every type the core maps, except <see cref="TimeSpan" /> and <see cref="DateTimeOffset" />, which not every
///     database has.
/// </summary>
public class Sample
{
    public int Id { get; set; }
    public byte Byte { get; set; }
    public short Short { get; set; }
    public long Long { get; set; }
    public float Single { get; set; }
    public double Double { get; set; }
    public decimal Decimal { get; set; }
    public bool Flag { get; set; }
    public string Text { get; set; } = "";
    public DateTime At { get; set; }
    public DateOnly Day { get; set; }
    public TimeOnly Time { get; set; }
    public Guid Uid { get; set; }
    public byte[] Data { get; set; } = [];
}

public class OffsetSample
{
    public int Id { get; set; }
    public DateTimeOffset At { get; set; }
}

public class FloatSample
{
    public int Id { get; set; }
    public double Double { get; set; }
    public float Single { get; set; }
}

public class FloatSampleContext(DbContextOptions<FloatSampleContext> options) : DbContext(options)
{
    public DbSet<FloatSample> FloatSamples => Set<FloatSample>();
}

public class SpanSample
{
    public int Id { get; set; }
    public TimeSpan Span { get; set; }
}

public class SpanSampleContext(DbContextOptions<SpanSampleContext> options) : DbContext(options)
{
    public DbSet<SpanSample> SpanSamples => Set<SpanSample>();
}

public class SampleContext(DbContextOptions<SampleContext> options) : DbContext(options)
{
    public DbSet<Sample> Samples => Set<Sample>();
}

public class OffsetSampleContext(DbContextOptions<OffsetSampleContext> options) : DbContext(options)
{
    public DbSet<OffsetSample> OffsetSamples => Set<OffsetSample>();
}

/// <summary>A level the database itself keeps between 0 and 10, with a check constraint.</summary>
public class Gauge
{
    public int Id { get; set; }
    public int Level { get; set; }
}

/// <summary>
///     A <see cref="DateTime" /> stored in the database's date type, so a value written as the type .NET would guess for it
///     (a timestamp) doesn't fit the column.
/// </summary>
public class Reading
{
    public int Id { get; set; }
    public DateTime Day { get; set; }
    public long Total { get; set; }
}

public class GaugeContext(DbContextOptions<GaugeContext> options) : DbContext(options)
{
    public DbSet<Gauge> Gauges => Set<Gauge>();
    public DbSet<Reading> Readings => Set<Reading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dialect = this.GetService<IAdoNetDialect>();
        var level = dialect.Sql.IdentifierQuotePrefix + "Level" + dialect.Sql.IdentifierQuoteSuffix;
        modelBuilder.Entity<Gauge>().ToTable(t => t.HasCheckConstraint("CK_Gauges_Level", $"{level} BETWEEN 0 AND 10"));
        modelBuilder.Entity<Reading>().Property(r => r.Day).HasColumnType(dialect.TypeMappings.GetStoreType(typeof(DateOnly)));
    }
}

/// <summary>A decimal wider than <see cref="long" />, at the widest precision every exact decimal type takes, and an ANSI string.</summary>
public class LedgerEntry
{
    public int Id { get; set; }
    public decimal Big { get; set; }

    [MaxLength(50)]
    [Unicode(false)]
    public string Code { get; set; } = "";
}

public class LedgerContext(DbContextOptions<LedgerContext> options) : DbContext(options)
{
    public DbSet<LedgerEntry> Entries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<LedgerEntry>().Property(e => e.Big).HasPrecision(38, 0);
}

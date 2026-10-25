using Microsoft.EntityFrameworkCore;

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

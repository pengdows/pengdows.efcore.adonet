using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     Constants written into SQL (<see cref="EF.Constant{T}" />) must mean what the same value bound as a parameter means:
///     each is matched against a row saved through parameters. Values sit at the edges the databases' types share:
///     fractional seconds, a long beyond a double's precision, a time with a fraction, an offset that moves the date.
/// </summary>
public abstract class LiteralConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    private static readonly Sample Seeded = new()
    {
        Byte = 200,
        Short = -2,
        Long = 9_007_199_254_740_993,
        Single = 1.5f,
        Double = 2.25,
        Decimal = 12.37m,
        Flag = true,
        Text = "it's",
        At = new DateTime(2024, 3, 5, 6, 7, 8, 123, 456),
        Day = new DateOnly(2024, 3, 5),
        Time = new TimeOnly(6, 7, 8, 500),
        Uid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
        Data = [0, 1, 255]
    };

    private static readonly TimeSpan SeededSpan = new(0, 1, 2, 3, 500);

    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();
        await Fixture.CreateTablesOnceAsync<SampleContext>(o => new(o));
        await using var context = Fixture.CreateContext<SampleContext>(o => new(o));
        await context.Samples.ExecuteDeleteAsync(Ct);
        context.Add(Copy(Seeded));
        await context.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private static Sample Copy(Sample s)
        => new()
        {
            Byte = s.Byte, Short = s.Short, Long = s.Long, Single = s.Single, Double = s.Double, Decimal = s.Decimal,
            Flag = s.Flag, Text = s.Text, At = s.At, Day = s.Day, Time = s.Time, Uid = s.Uid, Data = s.Data
        };

    [Fact]
    public async Task Constants_match_the_values_parameters_stored()
    {
        var mismatches = new List<string>();
        await Match(nameof(Sample.Byte), s => s.Byte == EF.Constant(Seeded.Byte), mismatches);
        await Match(nameof(Sample.Short), s => s.Short == EF.Constant(Seeded.Short), mismatches);
        await Match(nameof(Sample.Long), s => s.Long == EF.Constant(Seeded.Long), mismatches);
        await Match(nameof(Sample.Single), s => s.Single == EF.Constant(Seeded.Single), mismatches);
        await Match(nameof(Sample.Double), s => s.Double == EF.Constant(Seeded.Double), mismatches);
        await Match(nameof(Sample.Decimal), s => s.Decimal == EF.Constant(Seeded.Decimal), mismatches);
        await Match(nameof(Sample.Flag), s => s.Flag == EF.Constant(Seeded.Flag), mismatches);
        await Match(nameof(Sample.Text), s => s.Text == EF.Constant(Seeded.Text), mismatches);
        await Match(nameof(Sample.At), s => s.At == EF.Constant(Seeded.At), mismatches);
        await Match(nameof(Sample.Day), s => s.Day == EF.Constant(Seeded.Day), mismatches);
        await Match(nameof(Sample.Time), s => s.Time == EF.Constant(Seeded.Time), mismatches);
        await Match(nameof(Sample.Uid), s => s.Uid == EF.Constant(Seeded.Uid), mismatches);
        await Match(nameof(Sample.Data), s => s.Data == EF.Constant(Seeded.Data), mismatches);

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public async Task Time_span_constants()
    {
        SkipUnlessMapped(typeof(TimeSpan), "no interval type");

        await Fixture.CreateTablesOnceAsync<SpanSampleContext>(o => new(o));
        await using var context = Fixture.CreateContext<SpanSampleContext>(o => new(o));
        await context.SpanSamples.ExecuteDeleteAsync(Ct);
        context.Add(new SpanSample { Span = SeededSpan });
        await context.SaveChangesAsync(Ct);

        Assert.Equal(1, await context.SpanSamples.CountAsync(s => s.Span == EF.Constant(SeededSpan), Ct));
    }

    [Fact]
    public async Task Date_offset_constants_and_parts()
    {
        SkipUnlessMapped(typeof(DateTimeOffset), "no date and time type with an offset");

        // An offset that puts the local date on another day than UTC's, where the driver accepts one.
        var seeded = new DateTimeOffset(2024, 3, 5, 1, 0, 0, 500, Fixture.StorableOffset);
        await Fixture.CreateTablesOnceAsync<OffsetSampleContext>(o => new(o));
        await using var context = Fixture.CreateContext<OffsetSampleContext>(o => new(o));
        await context.OffsetSamples.ExecuteDeleteAsync(Ct);
        context.Add(new OffsetSample { At = seeded });
        await context.SaveChangesAsync(Ct);
        context.ChangeTracker.Clear();

        Assert.Equal(1, await context.OffsetSamples.CountAsync(s => s.At == EF.Constant(seeded), Ct));

        // The parts of the value as it reads back, whatever offset the database keeps.
        var stored = (await context.OffsetSamples.SingleAsync(Ct)).At;
        var parts = await context.OffsetSamples
            .Select(s => new { s.At.Year, s.At.Month, s.At.Day, s.At.Hour, s.At.Minute, s.At.Second })
            .Distinct()
            .SingleAsync(Ct);
        Assert.Equal(new { stored.Year, stored.Month, stored.Day, stored.Hour, stored.Minute, stored.Second }, parts);
    }

    private void SkipUnlessMapped(Type clrType, string reason)
    {
        if (Fixture.ExpectedDialect.TypeMappings.GetStoreType(clrType) == null)
        {
            Assert.Skip($"[SKIP:{Fixture.ExpectedDialect.Name}] {reason}");
        }
    }

    private async Task Match(string property, Expression<Func<Sample, bool>> predicate, List<string> mismatches)
    {
        await using var context = Fixture.CreateContext<SampleContext>(o => new(o));
        try
        {
            var count = await context.Samples.CountAsync(predicate, Ct);
            if (count != 1)
            {
                mismatches.Add($"{property}: {context.Samples.Where(predicate).ToQueryString()}");
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            mismatches.Add($"{property}: {e.GetType().Name}: {e.Message}");
        }
    }
}

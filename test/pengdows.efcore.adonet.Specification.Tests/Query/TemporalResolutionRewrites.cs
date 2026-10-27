using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;

namespace pengdows.efcore.adonet.Specification.Tests;

internal static class AdoNetTemporalData
{
    public static DateTimeOffset Normalize<THost>(DateTimeOffset value)
        where THost : IDatabaseHost, new()
    {
        if (new THost().KeepsOffsets)
        {
            return value;
        }

        var ticks = value.UtcDateTime.Ticks;
        var resolution = new THost().TemporalResolution.Ticks;
        return new DateTimeOffset(ticks - ticks % resolution, TimeSpan.Zero);
    }

    public static void NormalizeMissions<THost>(Microsoft.EntityFrameworkCore.TestUtilities.ISetSource data)
        where THost : IDatabaseHost, new()
    {
        var resolution = new THost().TemporalResolution.Ticks;
        foreach (var mission in data.Set<Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel.Mission>())
        {
            var utcTicks = mission.Timeline.UtcDateTime.Ticks;
            mission.Timeline = new DateTimeOffset(utcTicks - utcTicks % resolution, TimeSpan.Zero);
            var durationTicks = mission.Duration.Ticks;
            mission.Duration = new TimeSpan(durationTicks - durationTicks % resolution);
        }
    }
}

// EF's Nanosecond tests look for a value's 400 nanoseconds. A database that keeps microseconds holds none (BasicTypesAdoNetFixture
// cuts EF's data to what it keeps), so there every value's nanoseconds are 0: the test asks for that instead, which still checks
// the translation against every row. Where the database keeps 100-nanosecond ticks, EF's test runs as written.

public abstract partial class TimeOnlyTranslationsAdoNetTest<THost>
{
    [ConditionalFact]
    public override Task Nanosecond()
        => new THost().TemporalResolution.Ticks == 1
            ? base.Nanosecond()
            : AssertQuery(ss => ss.Set<BasicTypesEntity>().Where(e => e.TimeOnly.Nanosecond == 0));
}

public abstract partial class TimeSpanTranslationsAdoNetTest<THost>
{
    [ConditionalFact]
    public override Task Nanoseconds()
        => new THost().TemporalResolution.Ticks == 1
            ? base.Nanoseconds()
            : AssertQuery(ss => ss.Set<BasicTypesEntity>().Where(b => b.TimeSpan.Nanoseconds == 0));
}

public abstract partial class DateTimeOffsetTranslationsAdoNetTest<THost>
{
    [ConditionalFact]
    public override Task Nanosecond()
        => new THost().TemporalResolution.Ticks == 1
            ? base.Nanosecond()
            : AssertQuery(ss => ss.Set<BasicTypesEntity>().Where(e => e.DateTimeOffset.Nanosecond == 0));
}

public abstract partial class GearsOfWarQueryAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
    {
        if (new THost().KeepsOffsets)
        {
            return base.DateTimeOffset_Contains_Less_than_Greater_than(async);
        }

        var dto = AdoNetTemporalData.Normalize<THost>(new DateTimeOffset(599898024001234567, new TimeSpan(1, 30, 0)));
        var start = dto.AddDays(-1);
        var end = dto.AddDays(1);
        var dates = new[] { dto };
        return AssertQuery(async, ss => ss.Set<Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel.Mission>()
            .Where(m => start <= m.Timeline.Date && m.Timeline < end && dates.Contains(m.Timeline)));
    }

    public partial class AdoNetFixture
    {
        private bool _temporalDataNormalized;

        public override Microsoft.EntityFrameworkCore.TestUtilities.ISetSource GetExpectedData()
        {
            var data = base.GetExpectedData();
            if (_temporalDataNormalized || new THost().KeepsOffsets)
            {
                return data;
            }

            AdoNetTemporalData.NormalizeMissions<THost>(data);
            _temporalDataNormalized = true;
            return data;
        }
    }
}

public abstract partial class TPCGearsOfWarQueryAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
    {
        if (new THost().KeepsOffsets)
        {
            return base.DateTimeOffset_Contains_Less_than_Greater_than(async);
        }

        var dto = AdoNetTemporalData.Normalize<THost>(new DateTimeOffset(599898024001234567, new TimeSpan(1, 30, 0)));
        var start = dto.AddDays(-1);
        var end = dto.AddDays(1);
        var dates = new[] { dto };
        return AssertQuery(async, ss => ss.Set<Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel.Mission>()
            .Where(m => start <= m.Timeline.Date && m.Timeline < end && dates.Contains(m.Timeline)));
    }

    public partial class AdoNetFixture
    {
        private bool _temporalDataNormalized;

        public override Microsoft.EntityFrameworkCore.TestUtilities.ISetSource GetExpectedData()
        {
            var data = base.GetExpectedData();
            if (!_temporalDataNormalized && !new THost().KeepsOffsets)
            {
                AdoNetTemporalData.NormalizeMissions<THost>(data);
                _temporalDataNormalized = true;
            }

            return data;
        }
    }
}

public abstract partial class TPTGearsOfWarQueryAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
    {
        if (new THost().KeepsOffsets)
        {
            return base.DateTimeOffset_Contains_Less_than_Greater_than(async);
        }

        var dto = AdoNetTemporalData.Normalize<THost>(new DateTimeOffset(599898024001234567, new TimeSpan(1, 30, 0)));
        var start = dto.AddDays(-1);
        var end = dto.AddDays(1);
        var dates = new[] { dto };
        return AssertQuery(async, ss => ss.Set<Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel.Mission>()
            .Where(m => start <= m.Timeline.Date && m.Timeline < end && dates.Contains(m.Timeline)));
    }

    public partial class AdoNetFixture
    {
        private bool _temporalDataNormalized;

        public override Microsoft.EntityFrameworkCore.TestUtilities.ISetSource GetExpectedData()
        {
            var data = base.GetExpectedData();
            if (!_temporalDataNormalized && !new THost().KeepsOffsets)
            {
                AdoNetTemporalData.NormalizeMissions<THost>(data);
                _temporalDataNormalized = true;
            }

            return data;
        }
    }
}

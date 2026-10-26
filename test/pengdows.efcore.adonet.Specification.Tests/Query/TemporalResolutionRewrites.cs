using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;

namespace pengdows.efcore.adonet.Specification.Tests;

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

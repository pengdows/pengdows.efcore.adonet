using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class DateTimeOffsetTranslationsAdoNetTest<THost>
{
    [ConditionalFact]
    public override Task Nanosecond()
        => new THost().TemporalResolution.Ticks == 1
            ? base.Nanosecond()
            : AssertQuery(ss => ss.Set<BasicTypesEntity>().Where(e => e.DateTimeOffset.Nanosecond == 0));
}

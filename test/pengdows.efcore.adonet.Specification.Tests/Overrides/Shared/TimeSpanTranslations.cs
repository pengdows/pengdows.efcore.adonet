using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class TimeSpanTranslationsAdoNetTest<THost>
{
    [ConditionalFact]
    public override Task Nanoseconds()
        => new THost().TemporalResolution.Ticks == 1
            ? base.Nanoseconds()
            : AssertQuery(ss => ss.Set<BasicTypesEntity>().Where(b => b.TimeSpan.Nanoseconds == 0));
}

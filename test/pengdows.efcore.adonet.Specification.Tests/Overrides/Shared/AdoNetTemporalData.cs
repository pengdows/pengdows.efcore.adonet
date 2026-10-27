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

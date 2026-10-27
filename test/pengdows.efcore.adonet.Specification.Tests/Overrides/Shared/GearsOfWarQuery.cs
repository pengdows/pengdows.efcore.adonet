using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;

namespace pengdows.efcore.adonet.Specification.Tests;

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

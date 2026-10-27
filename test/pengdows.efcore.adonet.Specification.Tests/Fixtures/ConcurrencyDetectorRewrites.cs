using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class ConcurrencyDetectorDisabledAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public partial class AdoNetFixture
    {
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder).EnableThreadSafetyChecks(enableChecks: false);
    }
}

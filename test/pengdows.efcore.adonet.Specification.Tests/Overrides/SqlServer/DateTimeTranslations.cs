using System.Threading.Tasks;

namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public partial class DateTimeTranslationsSqlServerTest
{
    public override Task subtract_and_TotalDays()
        => AssertTranslationFailed(base.subtract_and_TotalDays);
}

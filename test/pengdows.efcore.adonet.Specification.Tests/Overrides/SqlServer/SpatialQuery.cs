using System.Threading.Tasks;

namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public partial class SpatialQuerySqlServerTest
{
    // These operations are intentionally not translated by SQL Server's own provider.
    public override Task CoveredBy(bool async) => Task.CompletedTask;
    public override Task Covers(bool async) => Task.CompletedTask;
    public override Task Normalized(bool async) => Task.CompletedTask;
    public override Task Reverse(bool async) => Task.CompletedTask;
    public override Task Union_void(bool async) => Task.CompletedTask;
    public override Task Distance_constant_srid_4326(bool async) => Task.CompletedTask;
    public override Task Z(bool async) => Task.CompletedTask;
}

namespace pengdows.efcore.adonet.Specification.Tests.DuckDb;

[Collection(SingleWriter.DuckDb)]
public class ProxyGraphUpdatesChangeTrackingDuckDbTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<DuckDbHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<DuckDbHost>(fixture);

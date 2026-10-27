namespace pengdows.efcore.adonet.Specification.Tests.DuckDb;

[Collection(SingleWriter.DuckDb)]
public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingDuckDbTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<DuckDbHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<DuckDbHost>(fixture);

namespace pengdows.efcore.adonet.Specification.Tests.DuckDb;

[Collection(SingleWriter.DuckDb)]
public class ProxyGraphUpdatesLazyLoadingDuckDbTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<DuckDbHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<DuckDbHost>(fixture);

[Collection(SingleWriter.DuckDb)]
public class ProxyGraphUpdatesChangeTrackingDuckDbTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<DuckDbHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<DuckDbHost>(fixture);

[Collection(SingleWriter.DuckDb)]
public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingDuckDbTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<DuckDbHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<DuckDbHost>(fixture);

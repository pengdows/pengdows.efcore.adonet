namespace pengdows.efcore.adonet.Specification.Tests.FlatFile;

[Collection(SingleWriter.FlatFile)]
public class ProxyGraphUpdatesLazyLoadingFlatFileTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<FlatFileHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<FlatFileHost>(fixture);

[Collection(SingleWriter.FlatFile)]
public class ProxyGraphUpdatesChangeTrackingFlatFileTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<FlatFileHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<FlatFileHost>(fixture);

[Collection(SingleWriter.FlatFile)]
public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingFlatFileTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<FlatFileHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<FlatFileHost>(fixture);

namespace pengdows.efcore.adonet.Specification.Tests.FlatFile;

[Collection(SingleWriter.FlatFile)]
public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingFlatFileTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<FlatFileHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<FlatFileHost>(fixture);

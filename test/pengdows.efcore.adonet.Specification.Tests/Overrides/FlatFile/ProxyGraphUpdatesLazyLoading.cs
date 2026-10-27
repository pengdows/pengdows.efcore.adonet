namespace pengdows.efcore.adonet.Specification.Tests.FlatFile;

[Collection(SingleWriter.FlatFile)]
public class ProxyGraphUpdatesLazyLoadingFlatFileTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<FlatFileHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<FlatFileHost>(fixture);

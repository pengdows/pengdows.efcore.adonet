namespace pengdows.efcore.adonet.Specification.Tests.DuckDb;

[Collection(SingleWriter.DuckDb)]
public class ProxyGraphUpdatesLazyLoadingDuckDbTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<DuckDbHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<DuckDbHost>(fixture);

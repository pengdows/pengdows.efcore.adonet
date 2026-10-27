namespace pengdows.efcore.adonet.Specification.Tests.Sqlite;

[Collection(SingleWriter.Sqlite)]
public class ProxyGraphUpdatesLazyLoadingSqliteTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<SqliteHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<SqliteHost>(fixture);

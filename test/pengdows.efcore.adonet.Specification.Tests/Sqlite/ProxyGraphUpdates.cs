namespace pengdows.efcore.adonet.Specification.Tests.Sqlite;

[Collection(SingleWriter.Sqlite)]
public class ProxyGraphUpdatesLazyLoadingSqliteTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<SqliteHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<SqliteHost>(fixture);

[Collection(SingleWriter.Sqlite)]
public class ProxyGraphUpdatesChangeTrackingSqliteTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<SqliteHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<SqliteHost>(fixture);

[Collection(SingleWriter.Sqlite)]
public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingSqliteTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<SqliteHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<SqliteHost>(fixture);

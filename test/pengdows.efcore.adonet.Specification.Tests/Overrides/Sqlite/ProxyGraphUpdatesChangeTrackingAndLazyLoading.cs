namespace pengdows.efcore.adonet.Specification.Tests.Sqlite;

[Collection(SingleWriter.Sqlite)]
public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingSqliteTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<SqliteHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<SqliteHost>(fixture);

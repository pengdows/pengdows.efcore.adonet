namespace pengdows.efcore.adonet.Specification.Tests.Sqlite;

[Collection(SingleWriter.Sqlite)]
public class ProxyGraphUpdatesChangeTrackingSqliteTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<SqliteHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<SqliteHost>(fixture);

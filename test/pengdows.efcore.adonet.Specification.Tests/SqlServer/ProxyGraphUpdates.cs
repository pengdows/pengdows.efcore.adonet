namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public class ProxyGraphUpdatesLazyLoadingSqlServerTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<SqlServerHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<SqlServerHost>(fixture);

public class ProxyGraphUpdatesChangeTrackingSqlServerTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<SqlServerHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<SqlServerHost>(fixture);

public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingSqlServerTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<SqlServerHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<SqlServerHost>(fixture);

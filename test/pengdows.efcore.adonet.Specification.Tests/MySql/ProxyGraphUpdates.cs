namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public class ProxyGraphUpdatesLazyLoadingMySqlTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<MySqlHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<MySqlHost>(fixture);

public class ProxyGraphUpdatesChangeTrackingMySqlTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<MySqlHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<MySqlHost>(fixture);

public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingMySqlTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<MySqlHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<MySqlHost>(fixture);

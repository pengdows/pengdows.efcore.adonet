namespace pengdows.efcore.adonet.Specification.Tests.PostgreSql;

public class ProxyGraphUpdatesLazyLoadingPostgreSqlTest(ProxyGraphUpdatesAdoNetTest.LazyLoading<PostgreSqlHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.LazyLoading<PostgreSqlHost>(fixture);

public class ProxyGraphUpdatesChangeTrackingPostgreSqlTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<PostgreSqlHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<PostgreSqlHost>(fixture);

public class ProxyGraphUpdatesChangeTrackingAndLazyLoadingPostgreSqlTest(ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<PostgreSqlHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTrackingAndLazyLoading<PostgreSqlHost>(fixture);

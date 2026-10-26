using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     EF's ProxyGraphUpdatesTestBase, run as the upstream providers run it: once with lazy-loading proxies, once with
///     change-tracking proxies, once with both, each in its own database. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.SqlServer.FunctionalTests/GraphUpdates/ProxyGraphUpdatesSqlServerTest.cs
/// </summary>
public static class ProxyGraphUpdatesAdoNetTest
{
    public abstract class Base<THost, TFixture>(TFixture fixture) : global::Microsoft.EntityFrameworkCore.ProxyGraphUpdatesTestBase<TFixture>(fixture)
        where THost : IDatabaseHost, new()
        where TFixture : Base<THost, TFixture>.FixtureBase, new()
    {
        protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
            => facade.UseTransaction(transaction.GetDbTransaction());

        public abstract class FixtureBase : ProxyGraphUpdatesFixtureBase, ITestSqlLoggerFactory
        {
            public TestSqlLoggerFactory TestSqlLoggerFactory
                => (TestSqlLoggerFactory)ListLoggerFactory;

            protected override ITestStoreFactory TestStoreFactory
                => AdoNetTestStoreFactory<THost>.Instance;

            protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                => base.AddServices(serviceCollection.AddEntityFrameworkProxies());
        }
    }

    public abstract class LazyLoading<THost>(LazyLoading<THost>.ProxyFixture fixture) : Base<THost, LazyLoading<THost>.ProxyFixture>(fixture)
        where THost : IDatabaseHost, new()
    {
        protected override bool DoesLazyLoading
            => true;

        protected override bool DoesChangeTracking
            => false;

        public class ProxyFixture : FixtureBase
        {
            protected override string StoreName
                => "ProxyGraphLazyLoadingUpdates";

            public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                => base.AddOptions(builder.UseLazyLoadingProxies());
        }
    }

    public abstract class ChangeTracking<THost>(ChangeTracking<THost>.ProxyFixture fixture) : Base<THost, ChangeTracking<THost>.ProxyFixture>(fixture)
        where THost : IDatabaseHost, new()
    {
        // The test loads through lazy loading, which this configuration (change-tracking proxies only) doesn't have; EF's own
        // providers leave it out of this configuration too.
        [ConditionalFact(Skip = "[SKIP:EF Core] needs lazy-loading proxies, which the change-tracking-only configuration doesn't enable")]
        public override Task Save_two_entity_cycle_with_lazy_loading()
            => base.Save_two_entity_cycle_with_lazy_loading();

        protected override bool DoesLazyLoading
            => false;

        protected override bool DoesChangeTracking
            => true;

        public class ProxyFixture : FixtureBase
        {
            protected override string StoreName
                => "ProxyGraphChangeTrackingUpdates";

            public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                => base.AddOptions(builder.UseChangeTrackingProxies());
        }
    }

    public abstract class ChangeTrackingAndLazyLoading<THost>(ChangeTrackingAndLazyLoading<THost>.ProxyFixture fixture) : Base<THost, ChangeTrackingAndLazyLoading<THost>.ProxyFixture>(fixture)
        where THost : IDatabaseHost, new()
    {
        protected override bool DoesLazyLoading
            => true;

        protected override bool DoesChangeTracking
            => true;

        public class ProxyFixture : FixtureBase
        {
            protected override string StoreName
                => "ProxyGraphChangeTrackingAndLazyLoadingUpdates";

            public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                => base.AddOptions(builder.UseLazyLoadingProxies().UseChangeTrackingProxies());
        }
    }
}

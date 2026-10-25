namespace pengdows.efcore.adonet.Specification.Tests;

public class NorthwindQueryAdoNetFixture<THost> : NorthwindQueryRelationalFixture<NoopModelCustomizer>
    where THost : IDatabaseHost, new()
{
    protected override ITestStoreFactory TestStoreFactory
        => AdoNetTestStoreFactory<THost>.Instance;
}

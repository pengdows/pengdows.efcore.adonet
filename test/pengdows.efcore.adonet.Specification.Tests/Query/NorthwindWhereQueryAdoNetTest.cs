namespace pengdows.efcore.adonet.Specification.Tests;

public abstract class NorthwindWhereQueryAdoNetTest<THost>(NorthwindQueryAdoNetFixture<THost> fixture)
    : NorthwindWhereQueryRelationalTestBase<NorthwindQueryAdoNetFixture<THost>>(fixture)
    where THost : IDatabaseHost, new();

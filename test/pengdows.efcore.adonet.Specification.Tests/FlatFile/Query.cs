namespace pengdows.efcore.adonet.Specification.Tests.FlatFile;

public class NorthwindWhereQueryFlatFileTest(NorthwindQueryAdoNetFixture<FlatFileHost> fixture) : NorthwindWhereQueryAdoNetTest<FlatFileHost>(fixture);

public class NorthwindSelectQueryFlatFileTest(NorthwindQueryAdoNetFixture<FlatFileHost> fixture) : NorthwindSelectQueryAdoNetTest<FlatFileHost>(fixture);

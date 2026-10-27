namespace pengdows.efcore.adonet.Specification.Tests.FlatFile;

[Collection(SingleWriter.FlatFile)]
public class NorthwindWhereQueryFlatFileTest(NorthwindQueryAdoNetFixture<FlatFileHost> fixture) : NorthwindWhereQueryAdoNetTest<FlatFileHost>(fixture);

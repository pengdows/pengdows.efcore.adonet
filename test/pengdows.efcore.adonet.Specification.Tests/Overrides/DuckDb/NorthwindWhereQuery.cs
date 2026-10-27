namespace pengdows.efcore.adonet.Specification.Tests.DuckDb;

[Collection(SingleWriter.DuckDb)]
public class NorthwindWhereQueryDuckDbTest(NorthwindQueryAdoNetFixture<DuckDbHost> fixture) : NorthwindWhereQueryAdoNetTest<DuckDbHost>(fixture);

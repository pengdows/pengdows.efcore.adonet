namespace pengdows.efcore.adonet.Specification.Tests.Sqlite;

[Collection(SingleWriter.Sqlite)]
public class NorthwindWhereQuerySqliteTest(NorthwindQueryAdoNetFixture<SqliteHost> fixture) : NorthwindWhereQueryAdoNetTest<SqliteHost>(fixture);

[Collection(SingleWriter.Sqlite)]
public class NorthwindSelectQuerySqliteTest(NorthwindQueryAdoNetFixture<SqliteHost> fixture) : NorthwindSelectQueryAdoNetTest<SqliteHost>(fixture);

namespace pengdows.efcore.adonet.Specification.Tests.Sqlite;

public class NorthwindWhereQuerySqliteTest(NorthwindQueryAdoNetFixture<SqliteHost> fixture) : NorthwindWhereQueryAdoNetTest<SqliteHost>(fixture);

public class NorthwindSelectQuerySqliteTest(NorthwindQueryAdoNetFixture<SqliteHost> fixture) : NorthwindSelectQueryAdoNetTest<SqliteHost>(fixture);

namespace pengdows.efcore.adonet.Specification.Tests.PostgreSql;

public class NorthwindWhereQueryPostgreSqlTest(NorthwindQueryAdoNetFixture<PostgreSqlHost> fixture) : NorthwindWhereQueryAdoNetTest<PostgreSqlHost>(fixture);

public class NorthwindSelectQueryPostgreSqlTest(NorthwindQueryAdoNetFixture<PostgreSqlHost> fixture) : NorthwindSelectQueryAdoNetTest<PostgreSqlHost>(fixture);

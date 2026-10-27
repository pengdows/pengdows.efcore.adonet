namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public class NorthwindWhereQueryMySqlTest(NorthwindQueryAdoNetFixture<MySqlHost> fixture) : NorthwindWhereQueryAdoNetTest<MySqlHost>(fixture);

public partial class NorthwindSelectQueryMySqlTest(NorthwindQueryAdoNetFixture<MySqlHost> fixture) : NorthwindSelectQueryAdoNetTest<MySqlHost>(fixture);

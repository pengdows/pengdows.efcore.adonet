using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     MariaDB or MySQL, from the connection's own handshake: no query, so these only need the connection open, not any
///     scripted result.
/// </summary>
public class MySqlAutoDetectTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("10.11.6-MariaDB-1:10.11.6+maria~ubu2204", "MariaDB")]
    [InlineData("5.5.5-10.3.39-MariaDB", "MariaDB")]
    [InlineData("8.4.3", "MySQL")]
    [InlineData("8.0.35-log", "MySQL")]
    public void From_server_version_reads_the_handshake_string(string serverVersion, string expectedName)
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=MySql" };
        connection.SetServerVersion(serverVersion);
        connection.Open();

        var dialect = MySqlDialect.FromServerVersion(connection);

        Assert.Equal(expectedName, dialect.Name);
    }

    [Fact]
    public void From_server_version_cannot_tell_singlestore_apart_without_the_version_comment()
    {
        // Confirmed against a real server: SingleStore's handshake reports a MySQL-compatible VERSION() (5.7.32), with
        // nothing distinguishing it. Only @@version_comment names it.
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=MySql" };
        connection.SetServerVersion("5.7.32");
        connection.Open();

        Assert.Same(MySqlDialect.Instance, MySqlDialect.FromServerVersion(connection));
    }

    [Theory]
    [InlineData("SingleStoreDB source distribution (compatible; MySQL Enterprise & MySQL Commercial)", "SingleStore")]
    [InlineData("MySQL Community Server - GPL", "MySQL")]
    public void From_server_version_with_the_version_comment_tells_singlestore_apart(string versionComment, string expectedName)
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=MySql" };
        connection.SetServerVersion("5.7.32");
        connection.Open();

        var dialect = MySqlDialect.FromServerVersion(connection, versionComment);

        Assert.Equal(expectedName, dialect.Name);
    }

    [Fact]
    public void Auto_detect_opens_and_closes_its_own_connection()
    {
        var factory = new fakeDbFactory(SupportedDatabase.MySql);
        var connection = new fakeDbConnection();
        connection.SetServerVersion("10.11.6-MariaDB-1:10.11.6+maria~ubu2204");
        connection.SetScalarResultForCommand("SELECT @@version_comment", "mariadb.org binary distribution");
        factory.Connections.Add(connection);

        var dialect = MySqlDialect.AutoDetect(factory, "Data Source=fake;EmulatedProduct=MySql");

        Assert.Same(MySqlDialect.MariaDb, dialect);
    }

    [Fact]
    public async Task Auto_detect_async_matches_the_synchronous_result()
    {
        var factory = new fakeDbFactory(SupportedDatabase.MySql);
        var connection = new fakeDbConnection();
        connection.SetServerVersion("8.4.3");
        connection.SetScalarResultForCommand("SELECT @@version_comment", "MySQL Community Server - GPL");
        factory.Connections.Add(connection);

        var dialect = await MySqlDialect.AutoDetectAsync(factory, "Data Source=fake;EmulatedProduct=MySql", Ct);

        Assert.Same(MySqlDialect.Instance, dialect);
    }

    [Fact]
    public async Task Auto_detect_async_tells_singlestore_apart_from_its_mysql_compatible_handshake()
    {
        var factory = new fakeDbFactory(SupportedDatabase.MySql);
        var connection = new fakeDbConnection();
        connection.SetServerVersion("5.7.32");
        connection.SetScalarResultForCommand(
            "SELECT @@version_comment", "SingleStoreDB source distribution (compatible; MySQL Enterprise & MySQL Commercial)");
        factory.Connections.Add(connection);

        var dialect = await MySqlDialect.AutoDetectAsync(factory, "Data Source=fake;EmulatedProduct=MySql", Ct);

        Assert.Same(MySqlDialect.SingleStore, dialect);
    }
}

/// <summary>The handshake string a real server and driver actually send matches what the fakeDb cases assume.</summary>
public class MySqlAutoDetectExecutionTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [Fact]
    public async Task A_real_MySQL_server_is_detected_as_MySQL()
        => Assert.Same(
            MySqlDialect.Instance,
            await MySqlDialect.AutoDetectAsync(fixture.Factory, fixture.ConnectionString, TestContext.Current.CancellationToken));
}

public class MariaDbAutoDetectExecutionTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task A_real_MariaDB_server_is_detected_as_MariaDB()
        => Assert.Same(
            MySqlDialect.MariaDb,
            await MySqlDialect.AutoDetectAsync(fixture.Factory, fixture.ConnectionString, TestContext.Current.CancellationToken));
}

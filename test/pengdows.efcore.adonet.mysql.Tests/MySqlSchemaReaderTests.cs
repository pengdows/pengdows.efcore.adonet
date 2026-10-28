using Microsoft.EntityFrameworkCore.Scaffolding;
using pengdows.efcore.adonet.Scaffolding;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     An integer column reads back as the type migrations wrote. MySQL dropped integer display widths in 8.0.19 (but for
///     TINYINT(1) and ZEROFILL); MariaDB still reports them (<c>BIGINT(20)</c>), and the width means nothing to the values.
/// </summary>
public abstract class MySqlSchemaReaderTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : MySqlFixture
{
    [Fact]
    public async Task Integer_columns_read_back_without_a_display_width()
    {
        await fixture.ExecuteAsync("DROP TABLE IF EXISTS \"Widths\"");
        await fixture.ExecuteAsync(
            "CREATE TABLE \"Widths\" (\"Id\" BIGINT NOT NULL PRIMARY KEY, \"Small\" SMALLINT, \"Medium\" MEDIUMINT, \"Plain\" INT, "
            + "\"Unsigned\" INT UNSIGNED, \"Flag\" TINYINT(1), \"Tiny\" TINYINT, \"Padded\" INT(5) ZEROFILL)");

        await using var connection = fixture.Factory.CreateConnection()!;
        connection.ConnectionString = fixture.ConnectionString;
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var model = new AdoNetDatabaseModelFactory(fixture.ExpectedDialect).Create(connection, new DatabaseModelFactoryOptions(["Widths"]));
        var types = model.Tables.Single().Columns.ToDictionary(c => c.Name, c => c.StoreType);

        Assert.Equal("BIGINT", types["Id"]);
        Assert.Equal("SMALLINT", types["Small"]);
        Assert.Equal("MEDIUMINT", types["Medium"]);
        Assert.Equal("INT", types["Plain"]);
        Assert.Equal("INT UNSIGNED", types["Unsigned"]);
        Assert.Equal("TINYINT(1)", types["Flag"]);
        Assert.Equal("TINYINT", types["Tiny"]);
        Assert.Equal("INT(5) UNSIGNED ZEROFILL", types["Padded"]);
    }
}

public class MySqlConnectorSchemaReaderTests(MySqlFixture fixture) : MySqlSchemaReaderTests<MySqlFixture>(fixture);

public class MariaDbSchemaReaderTests(MariaDbFixture fixture) : MySqlSchemaReaderTests<MariaDbFixture>(fixture);

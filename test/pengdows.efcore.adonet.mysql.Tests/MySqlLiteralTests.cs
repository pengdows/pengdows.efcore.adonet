namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>TIME holds -838:59:59 to 838:59:59, as hours, minutes and seconds: days are written as hours.</summary>
public class MySqlLiteralTests
{
    public static TheoryData<TimeSpan, string> Literals => new()
    {
        { new TimeSpan(0, 1, 2, 3), "TIME '01:02:03'" },
        { new TimeSpan(0, 1, 2, 3, 500), "TIME '01:02:03.500000'" },
        { new TimeSpan(1, 2, 0, 0), "TIME '26:00:00'" },
        { -new TimeSpan(1, 1, 30, 0), "TIME '-25:30:00'" },
        { new TimeSpan(34, 22, 59, 59), "TIME '838:59:59'" }
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void Time_spans_are_written_as_hours(TimeSpan value, string literal)
        => Assert.Equal(literal, MySqlDialect.Instance.TypeMappings.FormatLiteral(value, "TIME(6)", "unused"));

    [Fact]
    public void Time_spans_outside_the_time_range_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MySqlDialect.Instance.TypeMappings.FormatLiteral(new TimeSpan(35, 0, 0, 0), "TIME(6)", "unused"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MySqlDialect.Instance.TypeMappings.FormatLiteral(-new TimeSpan(35, 0, 0, 0), "TIME(6)", "unused"));
    }
}

/// <summary>
///     Backslash escapes string characters in MySQL unless the server's sql_mode has NO_BACKSLASH_ESCAPES; a dialect for
///     each, since doubling a backslash in that mode stores two.
/// </summary>
public class MySqlBackslashTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [Fact]
    public void Backslashes_are_doubled_only_where_they_escape()
    {
        Assert.Equal(@"'a\\b''c'", MySqlDialect.Instance.Sql.StringLiteral(@"a\b'c"));
        Assert.Equal(@"'a\b''c'", MySqlDialect.NoBackslashEscapes.Sql.StringLiteral(@"a\b'c"));
    }

    [Fact]
    public async Task A_server_without_backslash_escapes_matches_backslash_constants()
    {
        // Testcontainers' root has the user's password; SET GLOBAL needs it.
        var root = new MySqlConnector.MySqlConnectionStringBuilder(fixture.ConnectionString) { UserID = "root" };
        await using (var connection = new MySqlConnector.MySqlConnection(root.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new MySqlConnector.MySqlCommand("SET GLOBAL sql_mode = CONCAT(@@GLOBAL.sql_mode, ',NO_BACKSLASH_ESCAPES')", connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await fixture.ResetAsync();
        await fixture.ExecuteAsync(
            "INSERT INTO `Blogs` (`Name`, `IsActive`, `Rating`, `CreatedAt`, `ExternalId`) "
            + "VALUES ('a\\b', 1, 1, '2024-01-01', '0f8fad5b-d9cb-469f-a165-70867728950e')");

        await using var context = new pengdows.efcore.adonet.Testing.BlogContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<pengdows.efcore.adonet.Testing.BlogContext>()
                .UseAdoNet(MySqlConnector.MySqlConnectorFactory.Instance, fixture.ConnectionString, MySqlDialect.NoBackslashEscapes)
                .Options);

        Assert.Equal(1, await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            context.Blogs, b => b.Name == Microsoft.EntityFrameworkCore.EF.Constant(@"a\b"), TestContext.Current.CancellationToken));
    }
}

/// <summary>MySQL's CAST takes only its own targets (SIGNED, UNSIGNED, CHAR, DECIMAL, DOUBLE...), not column types.</summary>
public class MySqlCastTests
{
    [Theory]
    [InlineData("BIGINT", "SIGNED")]
    [InlineData("INT", "SIGNED")]
    [InlineData("INTEGER", "SIGNED")]
    [InlineData("SMALLINT", "SIGNED")]
    [InlineData("TINYINT(1)", "SIGNED")]
    [InlineData("TINYINT UNSIGNED", "UNSIGNED")]
    [InlineData("BIGINT UNSIGNED", "UNSIGNED")]
    [InlineData("DECIMAL(18,2)", "DECIMAL(18,2)")]
    [InlineData("DOUBLE", "DOUBLE")]
    [InlineData("FLOAT", "FLOAT")]
    [InlineData("LONGTEXT", "CHAR")]
    [InlineData("VARCHAR(50)", "CHAR(50)")]
    [InlineData("CHAR(36)", "CHAR(36)")]
    [InlineData("DATETIME(6)", "DATETIME(6)")]
    [InlineData("DATE", "DATE")]
    [InlineData("TIME(6)", "TIME(6)")]
    [InlineData("LONGBLOB", "BINARY")]
    [InlineData("VARBINARY(16)", "BINARY(16)")]
    [InlineData("JSON", "JSON")]
    public void Casts_name_mysqls_targets(string storeType, string target)
        => Assert.Equal(target, MySqlDialect.Instance.TypeMappings.CastTarget(storeType));
}

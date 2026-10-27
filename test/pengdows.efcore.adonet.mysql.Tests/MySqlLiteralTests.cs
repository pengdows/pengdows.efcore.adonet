using Microsoft.EntityFrameworkCore;

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

    // Inside single quotes a double quote is plain text, with or without ANSI_QUOTES: only the backslash and the quote escape.
    [Fact]
    public void Double_quotes_are_left_as_they_are()
    {
        Assert.Equal("'a\"b'", MySqlDialect.Instance.Sql.StringLiteral("a\"b"));
        Assert.Equal("'a\"b'", MySqlDialect.NoBackslashEscapes.Sql.StringLiteral("a\"b"));
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
        Assert.DoesNotContain(
            "NO_BACKSLASH_ESCAPES",
            Convert.ToString(await fixture.ExecuteScalarAsync("SELECT @@SESSION.sql_mode"), System.Globalization.CultureInfo.InvariantCulture));
        await using (var insertConnection = new MySqlConnector.MySqlConnection(fixture.ConnectionString))
        {
            await insertConnection.OpenAsync(TestContext.Current.CancellationToken);
            await using var setup = insertConnection.CreateCommand();
            setup.CommandText = MySqlDialect.NoBackslashEscapes.Sql.SessionSetup;
            await setup.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await using var insert = insertConnection.CreateCommand();
            insert.CommandText =
                "INSERT INTO `Blogs` (`Name`, `IsActive`, `Rating`, `CreatedAt`, `ExternalId`) "
                + "VALUES ('a\\b', 1, 1, '2024-01-01', '0f8fad5b-d9cb-469f-a165-70867728950e')";
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var context = new pengdows.efcore.adonet.Testing.BlogContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<pengdows.efcore.adonet.Testing.BlogContext>()
                .UseAdoNet(MySqlConnector.MySqlConnectorFactory.Instance, fixture.ConnectionString, MySqlDialect.NoBackslashEscapes)
                .Options);

        Assert.Equal(1, await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            context.Blogs, b => b.Name == Microsoft.EntityFrameworkCore.EF.Constant(@"a\b"), TestContext.Current.CancellationToken));
    }
}

/// <summary>
///     Each session adds ANSI_QUOTES, and sets NO_BACKSLASH_ESCAPES to match the string literals the dialect writes, leaving
///     every other mode the server had.
/// </summary>
public class MySqlSessionSetupTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private async Task<string[]> ModesAfter(string serverMode, IAdoNetDialect dialect)
    {
        await using var connection = new MySqlConnector.MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using (var set = connection.CreateCommand())
        {
            set.CommandText = $"SET SESSION sql_mode = '{serverMode}'";
            await set.ExecuteNonQueryAsync(Ct);
        }

        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = dialect.Sql.SessionSetup;
            await setup.ExecuteNonQueryAsync(Ct);
        }

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT @@SESSION.sql_mode";
        var modes = (string)(await read.ExecuteScalarAsync(Ct))!;
        return modes.Split(',', StringSplitOptions.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("STRICT_TRANS_TABLES")]
    [InlineData("NO_BACKSLASH_ESCAPES")]
    [InlineData("NO_BACKSLASH_ESCAPES,STRICT_TRANS_TABLES")]
    [InlineData("STRICT_TRANS_TABLES,NO_BACKSLASH_ESCAPES")]
    [InlineData("STRICT_TRANS_TABLES,NO_BACKSLASH_ESCAPES,ONLY_FULL_GROUP_BY")]
    public async Task A_dialect_that_escapes_backslashes_removes_the_mode_wherever_it_stands(string serverMode)
    {
        var modes = await ModesAfter(serverMode, MySqlDialect.Instance);

        var expected = serverMode.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(m => m != "NO_BACKSLASH_ESCAPES").Append("ANSI_QUOTES").Order();
        Assert.Equal(expected, modes.Order());
        Assert.DoesNotContain(string.Empty, modes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("STRICT_TRANS_TABLES,ONLY_FULL_GROUP_BY")]
    [InlineData("STRICT_TRANS_TABLES,NO_BACKSLASH_ESCAPES")]
    public async Task A_dialect_that_does_not_adds_the_mode(string serverMode)
    {
        var modes = await ModesAfter(serverMode, MySqlDialect.NoBackslashEscapes);

        var expected = serverMode.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(m => m != "NO_BACKSLASH_ESCAPES").Append("ANSI_QUOTES").Append("NO_BACKSLASH_ESCAPES").Order();
        Assert.Equal(expected, modes.Order());
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

/// <summary>The column type a sized or fixed-length property gets: fixed-length is CHAR and BINARY, however it is sized.</summary>
public class MySqlSizedStoreTypeTests
{
    [Theory]
    [InlineData(typeof(string), 10, false, "VARCHAR(10)")]
    [InlineData(typeof(string), 10, true, "CHAR(10)")]
    [InlineData(typeof(string), 16383, false, "VARCHAR(16383)")]
    [InlineData(typeof(string), 16384, false, "LONGTEXT")]
    [InlineData(typeof(string), -1, false, "LONGTEXT")]
    [InlineData(typeof(byte[]), 16, false, "VARBINARY(16)")]
    [InlineData(typeof(byte[]), 16, true, "BINARY(16)")]
    [InlineData(typeof(byte[]), 7999, false, "VARBINARY(7999)")]
    [InlineData(typeof(byte[]), 8000, false, "LONGBLOB")]
    public void A_sized_property_takes_the_type_its_size_and_length_kind_call_for(Type clrType, int size, bool fixedLength, string storeType)
        => Assert.Equal(storeType, MySqlDialect.Instance.TypeMappings.GetStoreType(clrType, new StoreTypeFacets(Size: size, FixedLength: fixedLength)));

    public class Coded
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public byte[] Hash { get; set; } = [];
        public string Name { get; set; } = "";
    }

    private sealed class CodedContext(DbContextOptions<CodedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Coded>(b =>
            {
                b.Property(e => e.Code).HasMaxLength(10).IsFixedLength();
                b.Property(e => e.Hash).HasMaxLength(16).IsFixedLength();
                b.Property(e => e.Name).HasMaxLength(40);
            });
    }

    [Fact]
    public void A_fixed_length_property_is_created_as_a_fixed_length_column()
    {
        using var context = new CodedContext(new DbContextOptionsBuilder<CodedContext>().UseAdoNetMySql("Server=localhost").Options);

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"Code\" CHAR(10) NOT NULL", script);
        Assert.Contains("\"Hash\" BINARY(16) NOT NULL", script);
        Assert.Contains("\"Name\" VARCHAR(40) NOT NULL", script);
    }
}

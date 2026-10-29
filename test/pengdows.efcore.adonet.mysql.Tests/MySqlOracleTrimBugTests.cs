using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Oracle's <c>MySql.EntityFrameworkCore</c> silently drops the trim-character argument for <c>Trim(char[])</c> of more
///     than one character, instead of refusing the call or trimming correctly: <c>ProcessTrimMethod</c>
///     (decompiled, 10.0.12) has no branch for <c>char[].Length &gt; 1</c>, so execution falls through to <c>TRIM(x)</c>,
///     which strips whitespace only. Confirmed here by running the real provider against MySQL 8.4; recorded in
///     UPSTREAM_ISSUES.md for filing. pengdows' own fix is <c>FunctionSyntax.TrimCharacterSet</c>, proven in
///     <c>MySqlUpstreamGapFakeDbTests.Trim_character_array_translates_to_a_regex_character_class</c>.
/// </summary>
public sealed class MySqlOracleTrimBugTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Oracles_provider_silently_ignores_the_trim_characters_and_strips_whitespace_only()
    {
        var database = "oracle_trim_bug_" + Guid.NewGuid().ToString("N");
        var connectionString = fixture.ConnectionStringFor(database) + ";AllowPublicKeyRetrieval=true";
        await using var context = new TrimBugContext(global::Microsoft.EntityFrameworkCore.MySQLDbContextOptionsExtensions.UseMySQL(new DbContextOptionsBuilder<TrimBugContext>().AllowManyDialects(), connectionString).Options);
        await context.Database.EnsureDeletedAsync(Ct);
        await context.Database.EnsureCreatedAsync(Ct);

        context.Rows.Add(new Row { Name = " AbA]xyz]Ab " });
        await context.SaveChangesAsync(Ct);

        // .NET's actual answer: every leading/trailing ' ', 'A', 'b' and ']' stripped, in any order, leaving "xyz".
        var expected = " AbA]xyz]Ab ".Trim(' ', 'A', 'b', ']');
        Assert.Equal("xyz", expected);

        var sql = context.Rows.Select(r => r.Name.Trim(' ', 'A', 'b', ']')).ToQueryString();
        var actual = await context.Rows.Select(r => r.Name.Trim(' ', 'A', 'b', ']')).SingleAsync(Ct);

        // What Oracle's provider actually returns: the requested characters are silently ignored, and only the
        // leading/trailing space (MySQL's plain TRIM(x)) is removed, leaving "AbA]xyz]Ab" instead of "xyz".
        Assert.Contains("TRIM(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REGEXP", sql, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(expected, actual);
        Assert.Equal("AbA]xyz]Ab", actual);
    }

    private sealed class TrimBugContext(DbContextOptions<TrimBugContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}

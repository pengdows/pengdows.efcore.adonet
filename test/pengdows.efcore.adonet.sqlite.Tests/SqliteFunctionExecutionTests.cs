using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     The translations of <see cref="SqliteFunctionTranslationTests" /> run through fakeDb emulating SQLite: the statement and
///     parameters sent, and the scripted values the database would return, materialized as their .NET types. What SQLite
///     computes is the integration suites' to prove.
/// </summary>
public class SqliteFunctionExecutionTests
{
    private readonly FakeSqlite _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private SampleContext Context()
        => _db.Context<SampleContext>(o => new SampleContext(o));

    [Fact]
    public async Task Glob_sends_its_pattern_as_a_parameter_and_reads_the_matching_rows()
    {
        await using var context = Context();
        var pattern = "[a-z]*'\"?";
        _db.Returns([7], [9]);

        var ids = await context.Samples.Where(x => EF.Functions.Glob(x.Text, pattern)).Select(x => x.Id).ToListAsync(Ct);

        Assert.Equal([7, 9], ids);
        Assert.Contains("\"s\".\"Text\" GLOB @pattern", Assert.Single(_db.Commands));
        Assert.Contains("@pattern='[a-z]*'\"?'", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task Hex_unhex_and_substr_read_back_text_and_bytes()
    {
        await using var context = Context();
        _db.Returns(["0AFF", new byte[] { 0x0A, 0xFF }, new byte[] { 0xFF }]);

        var row = await context.Samples
            .Select(x => new { Hex = EF.Functions.Hex(x.Data), Unhex = EF.Functions.Unhex(x.Text), Substr = EF.Functions.Substr(x.Data, 2, 1) })
            .SingleAsync(Ct);

        Assert.Equal("0AFF", row.Hex);
        Assert.Equal([0x0A, 0xFF], row.Unhex);
        Assert.Equal([0xFF], row.Substr);
        Assert.Contains("HEX(\"s\".\"Data\") AS \"Hex\", UNHEX(\"s\".\"Text\") AS \"Unhex\", SUBSTR(\"s\".\"Data\", 2, 1) AS \"Substr\"", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task An_unhex_of_text_that_is_not_hexadecimal_reads_back_null()
    {
        await using var context = Context();
        _db.Returns([(object?)null]);

        Assert.Null(await context.Samples.Select(x => EF.Functions.Unhex(x.Text)).SingleAsync(Ct));
    }

    [Fact]
    // fakeDb 2.0.6-rc2 converts SQLite text to DateTime but not to DateOnly or TimeSpan, nor a REAL to decimal, as
    // Microsoft.Data.Sqlite does (UPSTREAM_ISSUES.md); those values are scripted as the driver returns them.
    public async Task Date_and_time_arithmetic_reads_back_as_dates_and_times()
    {
        await using var context = Context();
        _db.Returns(["2024-03-05 07:37:08", new DateOnly(2024, 3, 8), new TimeSpan(0, 6, 7, 8, 500), 638451796280000000L]);

        var row = await context.Samples
            .Select(x => new { Later = x.At.AddHours(1.5), Day = x.Day.AddDays(3), x.At.TimeOfDay, x.At.Ticks })
            .SingleAsync(Ct);

        Assert.Equal(new DateTime(2024, 3, 5, 7, 37, 8), row.Later);
        Assert.Equal(new DateOnly(2024, 3, 8), row.Day);
        Assert.Equal(new TimeSpan(0, 6, 7, 8, 500), row.TimeOfDay);
        Assert.Equal(638451796280000000L, row.Ticks);
        Assert.Contains("DATE(\"s\".\"Day\", CAST(3 AS TEXT) || ' days')", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task Decimal_division_reads_back_the_fraction()
    {
        await using var context = Context();
        var divisor = 4m;
        _db.Returns([2.5m]);

        Assert.Equal(2.5m, await context.Samples.Select(x => x.Decimal / divisor).SingleAsync(Ct));
        Assert.Contains("CAST(\"s\".\"Decimal\" AS REAL) / @divisor", Assert.Single(_db.Commands));
        Assert.Contains("@divisor='4'", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task Regex_is_match_sends_regexp_with_the_pattern_as_a_parameter()
    {
        await using var context = Context();
        var pattern = "^a.*\\d$";
        _db.Returns([3]);

        var ids = await context.Samples.Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.Text, pattern)).Select(x => x.Id).ToListAsync(Ct);

        Assert.Equal([3], ids);
        Assert.Contains("(\"s\".\"Text\" REGEXP @pattern)", Assert.Single(_db.Commands));
    }

    /// <summary>
    ///     The regexp function is registered on a Microsoft.Data.Sqlite connection only; another connection, such as fakeDb's,
    ///     opens without it. Running REGEXP is the integration suites'.
    /// </summary>
    [Fact]
    public void The_function_interceptor_is_registered_and_skips_a_connection_of_another_driver()
    {
        using var context = Context();
        var interceptors = ((IInfrastructure<IServiceProvider>)context).Instance.GetServices<IInterceptor>();

        Assert.Contains(interceptors, i => i is SqliteConnectionFunctionsInterceptor);
        context.Database.OpenConnection();
        context.Database.CloseConnection();
    }

    [Fact]
    public async Task A_bool_as_text_reads_back_true_or_false()
    {
        await using var context = Context();
        _db.Returns(["True"]);

        Assert.Equal("True", await context.Samples.Select(x => x.Flag.ToString()).SingleAsync(Ct));
        Assert.Contains("CASE \"s\".\"Flag\" WHEN 0 THEN 'False' WHEN 1 THEN 'True' ELSE '' END", Assert.Single(_db.Commands));
    }
}

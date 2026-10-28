using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     Each .NET member the core translates, run on the server and compared with what .NET computes. Each is also compared in a
///     <c>WHERE</c>, which EF can't evaluate on the client, so an untranslated member fails instead of quietly passing
///     (a projection, even through <c>Distinct</c>, may be finished on the client).
/// </summary>
public abstract class TranslationConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    // Spaces to trim, LIKE wildcards (and SQL Server's [) and the usual escape character to match literally, mixed case.
    // The rating is no rounding midpoint: .NET rounds those to even, SQL away from zero. The time has a fraction of a second.
    private static readonly Blog Seeded = new()
    {
        Name = @"  Ab%_c\d[x]  ",
        Rating = 12.37m,
        IsActive = true,
        CreatedAt = new DateTime(2024, 3, 5, 6, 7, 8, 500, DateTimeKind.Unspecified),
        ExternalId = Guid.NewGuid()
    };

    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();

        await Fixture.ResetAsync();
        await using var context = Fixture.CreateContext();
        context.Add(new Blog
        {
            Name = Seeded.Name,
            Rating = Seeded.Rating,
            IsActive = Seeded.IsActive,
            CreatedAt = Seeded.CreatedAt,
            ExternalId = Seeded.ExternalId
        });
        await context.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private Task Server<T>(Expression<Func<Blog, T>> selector)
        => Server(selector, selector.Compile()(Seeded));

    // For a database function, which has no .NET implementation to compute the expected value with.
    private async Task Server<T>(Expression<Func<Blog, T>> selector, T expected)
    {
        await using var context = Fixture.CreateContext();

        var actual = await context.Blogs.Select(selector).Distinct().SingleAsync(Ct);
        Assert.Equal(expected, actual);

        // The server computed it: a predicate is never finished on the client.
        var computedOnServer = Expression.Lambda<Func<Blog, bool>>(
            Expression.Equal(selector.Body, Expression.Constant(actual, typeof(T))), selector.Parameters);
        Assert.Equal(1, await context.Blogs.CountAsync(computedOnServer, Ct));
    }

    private async Task Matches(Expression<Func<Blog, bool>> predicate)
    {
        await using var context = Fixture.CreateContext();

        Assert.Equal(predicate.Compile()(Seeded) ? 1 : 0, await context.Blogs.CountAsync(predicate, Ct));
    }

    [Fact]
    public Task Concatenation()
        => Server(b => b.Name + "!" + b.Name);

    [Fact]
    public async Task Case()
    {
        await Server(b => b.Name.ToUpper());
        await Server(b => b.Name.ToLower());
    }

    [Fact]
    public async Task Trimming()
    {
        await Server(b => b.Name.Trim());
        await Server(b => b.Name.TrimStart());
        await Server(b => b.Name.TrimEnd());
        await Server(b => b.Name.Trim(' '));
        await Server(b => b.Name.TrimStart(' '));
        await Server(b => b.Name.TrimEnd(' '));
    }

    // String.Trim(char[]) removes any of the characters, in any order: the database's trim of a set of characters, where
    // it has one (ISO's TRIM takes a single character).
    [Fact]
    public async Task Trimming_a_set_of_characters()
    {
        if (Fixture.ExpectedDialect.Sql.Functions.TrimCharacters == null)
        {
            Fixture.SkipFor("no trim of a set of characters (ISO's TRIM takes one)");
        }

        await Server(b => b.Name.Trim(' ', 'A', 'b', ']'));
        await Server(b => b.Name.TrimStart(' ', 'b', 'A'));
        await Server(b => b.Name.TrimEnd(']', ' ', 'x'));
        await Server(b => b.Name.Trim(' ', '%'));
    }

    // DateTime.UtcNow is the server's clock in UTC, read by the database (a predicate isn't evaluated on the client):
    // within a couple of minutes of this machine's, which the databases under test share.
    [Fact]
    public async Task Current_time_in_utc_is_the_servers()
    {
        if (Fixture.ExpectedDialect.Sql.Functions.UtcNow == null)
        {
            Fixture.SkipFor("no current time in UTC");
        }

        await using var context = Fixture.CreateContext();
        var now = await context.Blogs.Where(b => DateTime.UtcNow > b.CreatedAt).Select(b => DateTime.UtcNow).FirstAsync(Ct);

        Assert.InRange(now, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(2));
    }

    // String.PadLeft/PadRight: padded to the width with a space or the given character, and a string already as long kept
    // whole (SQL's LPAD/RPAD would cut it), where the database pads at all (ISO SQL has no padding function).
    [Fact]
    public async Task Padding()
    {
        if (Fixture.ExpectedDialect.Sql.Functions.PadLeft == null)
        {
            Fixture.SkipFor("no padding function (ISO SQL has none)");
        }

        await Server(b => b.Name.PadLeft(20));
        await Server(b => b.Name.PadRight(20));
        await Server(b => b.Name.PadLeft(20, '*'));
        await Server(b => b.Name.PadRight(17, '-'));
        await Server(b => b.Name.PadLeft(3));
        await Server(b => b.Name.PadRight(3, '-'));
    }

    // EF.Functions.DateDiff*, as SQL Server's and Pomelo's providers have them: whole units apart, so a database counting
    // boundaries crossed (SQL Server) and one counting whole units (MySQL) agree.
    [Fact]
    public async Task Date_differences()
    {
        if (Fixture.ExpectedDialect.Sql.Functions.DateDiff(DateDiffUnit.Day) == null)
        {
            Fixture.SkipFor("no date difference function");
        }

        await Server(b => EF.Functions.DateDiffYear(b.CreatedAt, new DateTime(2027, 3, 5, 6, 7, 8, 500)), 3);
        await Server(b => EF.Functions.DateDiffMonth(b.CreatedAt, new DateTime(2024, 6, 5, 6, 7, 8, 500)), 3);
        await Server(b => EF.Functions.DateDiffDay(b.CreatedAt, new DateTime(2024, 3, 15, 6, 7, 8, 500)), 10);
        await Server(b => EF.Functions.DateDiffHour(b.CreatedAt, new DateTime(2024, 3, 5, 11, 7, 8, 500)), 5);
        await Server(b => EF.Functions.DateDiffMinute(b.CreatedAt, new DateTime(2024, 3, 5, 6, 37, 8, 500)), 30);
        await Server(b => EF.Functions.DateDiffSecond(b.CreatedAt, new DateTime(2024, 3, 5, 6, 7, 50, 500)), 42);
        await Server(b => EF.Functions.DateDiffMillisecond(b.CreatedAt, new DateTime(2024, 3, 5, 6, 7, 9, 0)), 500);
        await Server(b => EF.Functions.DateDiffDay(new DateTime(2024, 3, 15, 6, 7, 8, 500), b.CreatedAt), -10);
    }

    [Fact]
    public Task Length()
        => Server(b => b.Name.Length);

    [Fact]
    public async Task Substring()
    {
        await Server(b => b.Name.Substring(1));
        await Server(b => b.Name.Substring(3));
        await Server(b => b.Name.Substring(2, 3));
    }

    [Fact]
    public async Task IndexOf()
    {
        await Server(b => b.Name.IndexOf("b%"));
        await Server(b => b.Name.IndexOf("zz"));
        await Server(b => b.Name.IndexOf(""));
    }

    [Fact]
    public Task Replace()
        => Server(b => b.Name.Replace("b%", "-"));

    [Fact]
    public async Task Constant_patterns_match_wildcards_literally()
    {
        await Matches(b => b.Name.StartsWith("  Ab%"));
        await Matches(b => b.Name.StartsWith("  A_"));
        await Matches(b => b.Name.EndsWith(@"\d  "));
        await Matches(b => b.Name.Contains(@"%_c\"));
        await Matches(b => b.Name.Contains("b%c"));
        await Matches(b => b.Name.Contains(""));
        await Matches(b => b.Name.Contains("[x]"));
        await Matches(b => b.Name.EndsWith("[x]  "));
    }

    [Fact]
    public async Task Parameter_patterns_match_wildcards_literally()
    {
        foreach (var value in new[] { "  Ab%", "  A_", @"\d  ", @"%_c\", "b%c", "", "longer than the name itself" })
        {
            await Matches(b => b.Name.StartsWith(value));
            await Matches(b => b.Name.EndsWith(value));
            await Matches(b => b.Name.Contains(value));
        }
    }

    [Fact]
    public async Task Null_or_white_space()
    {
        await Matches(b => string.IsNullOrWhiteSpace(b.Name));
        await Matches(b => !string.IsNullOrWhiteSpace(b.Name));
    }

    [Fact]
    public async Task Math_on_decimals()
    {
        await Server(b => Math.Abs(-b.Rating));
        await Server(b => Math.Floor(b.Rating));
        await Server(b => Math.Ceiling(b.Rating));
        await Server(b => Math.Round(b.Rating));
        await Server(b => Math.Round(b.Rating, 1));
    }

    /// <summary>
    ///     Bitwise operators on integers, as .NET computes them, negatives and the smallest int included. Covers EF's base
    ///     Northwind tests mixing them in projections (<c>Projection_when_arithmetic_mixed_subqueries</c>).
    /// </summary>
    [Fact]
    public async Task Bitwise_operators()
    {
        if (Fixture.ExpectedDialect.Sql.Functions.BitwiseAnd == null)
        {
            Fixture.SkipFor("no bitwise operators (ISO SQL has none)");
        }

        await Server(b => (int)b.Rating ^ 10);
        await Server(b => -(int)b.Rating ^ 5);
        await Server(b => ((int)b.Rating + 1) ^ 3);
        await Server(b => (int)b.Rating ^ int.MinValue);
        await Server(b => ((int)b.Rating & 6) | 1);
        await Server(b => ~(int)b.Rating);
    }

    [Fact]
    public async Task Math_on_doubles()
    {
        await Server(b => Math.Floor((double)b.Rating));
        await Server(b => Math.Round((double)b.Rating, 1));
        await Server(b => Math.Pow((double)b.Rating, 2) > 153);
        await Server(b => Math.Sqrt((double)b.Rating) > 3.5);
    }

    [Fact]
    public async Task Row_values()
    {
        if (!Fixture.ExpectedDialect.Sql.RowValues)
        {
            Fixture.SkipFor("no row value comparisons");
        }

        await Matches(b => new Tuple<string, bool>(b.Name, b.IsActive).Equals(new Tuple<string, bool>(Seeded.Name, true)));
        await Matches(b => new Tuple<string, bool>(b.Name, b.IsActive).Equals(new Tuple<string, bool>(Seeded.Name, false)));
        await Matches(b => !new Tuple<string, bool>(b.Name, b.IsActive).Equals(new Tuple<string, bool>("x", true)));
        await Matches(b => ValueTuple.Create(b.Rating, b.Name).Equals(ValueTuple.Create(12.37m, Seeded.Name)));
        await Matches(b => new Tuple<decimal>(b.Rating).Equals(new Tuple<decimal>(12.37m)));
    }

    [Fact]
    public async Task Date_parts()
    {
        await Server(b => b.CreatedAt.Year);
        await Server(b => b.CreatedAt.Month);
        await Server(b => b.CreatedAt.Day);
        await Server(b => b.CreatedAt.Hour);
        await Server(b => b.CreatedAt.Minute);
        await Server(b => b.CreatedAt.Second);
        await Matches(b => b.CreatedAt.Second == 8);
    }

    // A fraction of an hour, day or minute is kept: Pomelo rounds AddHours(1.5) to two hours (UPSTREAM_ISSUES.md).
    [Fact]
    public async Task Date_arithmetic()
    {
        var functions = Fixture.ExpectedDialect.Sql.Functions;
        if ((functions.AddMicroseconds == null) || (functions.AddMonths == null) || (functions.AddYears == null))
        {
            Fixture.SkipFor("no date arithmetic");
        }

        await Server(b => b.CreatedAt.AddHours(1.5));
        await Server(b => b.CreatedAt.AddDays(0.25));
        await Server(b => b.CreatedAt.AddMinutes(-90.5));
        await Server(b => b.CreatedAt.AddMilliseconds(250));
        await Server(b => b.CreatedAt.AddMonths(13));
        await Server(b => b.CreatedAt.AddYears(-1));
    }

    // A column is no constant to escape, so these compare the text itself; a null argument matches nothing, however it is
    // negated, as it does in .NET's own string methods' relational reading.
    [Fact]
    public async Task String_methods_with_a_column_argument_compare_the_text()
    {
        await Matches(b => b.Name.StartsWith(b.Name));
        await Matches(b => b.Name.EndsWith(b.Name));
        await Matches(b => b.Name.Contains(b.Name));
        await Matches(b => b.Name.StartsWith(b.Name.Trim()));
        await Matches(b => b.Name.Trim().StartsWith(b.Name));
        await Matches(b => !b.Name.Contains(b.Name + "x"));
    }

    [Fact]
    public async Task String_methods_with_a_null_argument_match_nothing()
    {
        await using var context = Fixture.CreateContext();

        Assert.Equal(0, await context.Blogs.CountAsync(b => b.Name.StartsWith(null!), Ct));
        Assert.Equal(0, await context.Blogs.CountAsync(b => b.Name.EndsWith(null!), Ct));
        Assert.Equal(0, await context.Blogs.CountAsync(b => b.Name.Contains(null!), Ct));
        Assert.Equal(1, await context.Blogs.CountAsync(b => !b.Name.Contains(null!), Ct));
    }

    // CreatedAt is 64.25 days (and a bit) after the origin.
    [Fact]
    public async Task The_difference_between_two_dates_in_days()
    {
        var functions = Fixture.ExpectedDialect.Sql.Functions;
        if ((functions.DateDifferenceTotalDays == null) && (functions.DateDifference == null))
        {
            Fixture.SkipFor("no date difference");
        }

        var origin = new DateTime(2024, 1, 1);
        await Matches(b => ((b.CreatedAt - origin).TotalDays > 64.25) && ((b.CreatedAt - origin).TotalDays < 64.26));
        await Matches(b => ((origin - b.CreatedAt).TotalDays < -64.25) && ((origin - b.CreatedAt).TotalDays > -64.26));
    }

    // 06:07:08.5 plus an hour, less an hour.
    [Fact]
    public async Task Time_spans_add_and_subtract()
    {
        var functions = Fixture.ExpectedDialect.Sql.Functions;
        if ((functions.TimeSpanAdd == null) || (functions.TimeSpanSubtract == null))
        {
            Fixture.SkipFor("no time span arithmetic");
        }

        await Server(b => (b.CreatedAt.TimeOfDay + TimeSpan.FromHours(1)).Hours);
        await Server(b => (b.CreatedAt.TimeOfDay - TimeSpan.FromHours(1)).Hours);
        await Server(b => (b.CreatedAt.TimeOfDay + TimeSpan.FromMinutes(54)).Minutes);
    }
}

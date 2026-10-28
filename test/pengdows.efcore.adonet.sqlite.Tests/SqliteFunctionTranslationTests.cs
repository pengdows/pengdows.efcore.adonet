using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     The SQL Microsoft's SQLite provider translates that relational EF Core doesn't: its <c>EF.Functions</c>, char and
///     string members, dates stored as text, binary values and decimals. SQL shape only, checked without a database.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/tree/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/Translators
/// </summary>
public class SqliteFunctionTranslationTests
{
    private static SampleContext Context()
        => new(new DbContextOptionsBuilder<SampleContext>().UseAdoNetSqlite("Data Source=:memory:").Options);

    private static string Sql<T>(Func<IQueryable<Sample>, IQueryable<T>> query)
    {
        using var context = Context();
        return query(context.Samples).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void Glob_matches_case_sensitively_with_unix_wildcards()
    {
        var sql = Sql(s => s.Where(x => EF.Functions.Glob(x.Text, "[a-z]*?\"'%_")));

        Assert.Contains("\"s\".\"Text\" GLOB '[a-z]*?\"''%_'", sql);
    }

    [Fact]
    public void Glob_negated_is_not_glob()
    {
        var sql = Sql(s => s.Where(x => !EF.Functions.Glob(x.Text, "a*")));

        Assert.Contains("NOT ((\"s\".\"Text\" GLOB 'a*'))", sql);
    }

    [Fact]
    public void Hex_and_unhex_convert_between_blobs_and_hexadecimal_text()
    {
        Assert.Contains("HEX(\"s\".\"Data\")", Sql(s => s.Select(x => EF.Functions.Hex(x.Data))));
        Assert.Contains("UNHEX(\"s\".\"Text\")", Sql(s => s.Select(x => EF.Functions.Unhex(x.Text))));
        Assert.Contains("UNHEX(\"s\".\"Text\", ' -')", Sql(s => s.Select(x => EF.Functions.Unhex(x.Text, " -"))));
    }

    [Fact]
    public void Substr_of_a_blob_takes_one_based_positions_as_given()
    {
        Assert.Contains("SUBSTR(\"s\".\"Data\", 2)", Sql(s => s.Select(x => EF.Functions.Substr(x.Data, 2))));
        Assert.Contains("SUBSTR(\"s\".\"Data\", 2, 3)", Sql(s => s.Select(x => EF.Functions.Substr(x.Data, 2, 3))));
    }

    [Fact]
    public void Functions_called_outside_a_query_throw()
    {
        Assert.Throws<InvalidOperationException>(() => EF.Functions.Glob("a", "a"));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.Hex([1]));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.Unhex("01"));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.Unhex("01", "-"));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.Substr([1], 1));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.Substr([1], 1, 1));
    }

    [Fact]
    public void Random_is_a_double_from_zero_to_one()
        => Assert.Contains("ABS(RANDOM() / 9223372036854780000.0)", Sql(s => s.Select(x => EF.Functions.Random())));

    [Fact]
    public void First_and_last_character_of_a_string_and_their_case()
    {
        Assert.Contains("UPPER(SUBSTR(\"s\".\"Text\", 1, 1))", Sql(s => s.Select(x => char.ToUpper(x.Text.FirstOrDefault()))));
        Assert.Contains(
            "LOWER(SUBSTR(\"s\".\"Text\", LENGTH(\"s\".\"Text\"), 1))",
            Sql(s => s.Select(x => char.ToLower(x.Text.LastOrDefault()))));
    }

    [Fact]
    public void IndexOf_from_a_position_is_minus_one_when_absent()
        => Assert.Contains(
            "CASE WHEN INSTR(SUBSTR(\"s\".\"Text\", 3), 'x') > 0 THEN INSTR(SUBSTR(\"s\".\"Text\", 3), 'x') + 3 - 1 ELSE 0 END - 1",
            Sql(s => s.Select(x => x.Text.IndexOf("x", 2))));

    [Fact]
    public void Ticks_counts_from_the_julian_day_of_year_one()
        => Assert.Contains(
            "CAST((JULIANDAY(\"s\".\"At\") - 1721425.5) * 864000000000.0 AS INTEGER)",
            Sql(s => s.Select(x => x.At.Ticks)));

    [Fact]
    public void Adding_hours_minutes_and_seconds_keeps_the_text_the_driver_writes()
    {
        var sql = Sql(s => s.Select(x => x.At.AddHours(1.5)));

        Assert.Contains("RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', \"s\".\"At\", CAST((1.5 * 3600000000.0) / 1000000.0 AS TEXT) || ' seconds'), '0'), '.')", sql);
    }

    [Fact]
    public void Adding_ticks_adds_seconds()
        => Assert.Contains(
            "RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', \"s\".\"At\", CAST(\"s\".\"Long\" / 10000000.0 AS TEXT) || ' seconds'), '0'), '.')",
            Sql(s => s.Select(x => x.At.AddTicks(x.Long))));

    [Fact]
    public void Adding_to_a_date_stays_a_date()
    {
        Assert.Contains("DATE(\"s\".\"Day\", CAST(3 AS TEXT) || ' days')", Sql(s => s.Select(x => x.Day.AddDays(3))));
        Assert.Contains("DATE(\"s\".\"Day\", CAST(2 AS TEXT) || ' months')", Sql(s => s.Select(x => x.Day.AddMonths(2))));
        Assert.Contains("DATE(\"s\".\"Day\", CAST(1 AS TEXT) || ' years')", Sql(s => s.Select(x => x.Day.AddYears(1))));
    }

    [Fact]
    public void A_date_from_a_date_and_time_is_the_date_function()
        => Assert.Contains("DATE(\"s\".\"At\")", Sql(s => s.Select(x => DateOnly.FromDateTime(x.At))));

    [Fact]
    public void Time_of_day_is_the_time_without_trailing_zeros()
        => Assert.Contains(
            "RTRIM(RTRIM(STRFTIME('%H:%M:%f', \"s\".\"At\"), '0'), '.')",
            Sql(s => s.Select(x => x.At.TimeOfDay)));

    [Fact]
    public void Byte_array_length_first_byte_and_contains()
    {
        Assert.Contains("LENGTH(\"s\".\"Data\")", Sql(s => s.Select(x => x.Data.Length)));
        Assert.Contains("INSTR(\"s\".\"Data\", UNHEX(PRINTF('%02X', 7))) > 0", Sql(s => s.Where(x => x.Data.Contains((byte)7))));
        Assert.Contains("HEX(SUBSTR(\"s\".\"Data\", 1, 1))", Sql(s => s.Select(x => x.Data.First())));
    }

    [Fact]
    public void Regex_is_match_is_the_regexp_operator()
        => Assert.Contains("(\"s\".\"Text\" REGEXP '^a.*\\d$')", Sql(s => s.Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.Text, "^a.*\\d$"))));

    [Fact]
    public void A_bool_as_text_is_true_or_false()
        => Assert.Contains(
            "CASE \"s\".\"Flag\" WHEN 0 THEN 'False' WHEN 1 THEN 'True' ELSE '' END",
            Sql(s => s.Select(x => x.Flag.ToString())));

    [Fact]
    public void A_date_or_double_as_text_is_never_null()
    {
        Assert.Contains("COALESCE(CAST(\"s\".\"At\" AS TEXT), '')", Sql(s => s.Select(x => x.At.ToString())));
        Assert.Contains("COALESCE(CAST(\"s\".\"Double\" AS TEXT), '')", Sql(s => s.Select(x => x.Double.ToString())));
    }

    // SQLite stores a decimal with NUMERIC affinity: whole values as INTEGER, others as REAL, so comparison, ordering and
    // aggregates are the database's own and need no functions registered on the connection, where Microsoft's provider stores
    // decimals as TEXT and registers ef_compare, ef_add and the rest. Two whole values would divide as integers, so a decimal
    // division is made REAL; SQLite's % truncates its operands to integers, so a fractional remainder is MOD.
    [Fact]
    public void Decimal_comparison_ordering_and_aggregates_are_native()
    {
        var sql = Sql(s => s.Where(x => x.Decimal > 1.5m).OrderBy(x => x.Decimal).Select(x => x.Decimal + x.Decimal * 2m));

        Assert.Contains("\"s\".\"Decimal\" > 1.5", sql);
        Assert.Contains("ORDER BY \"s\".\"Decimal\"", sql);
        Assert.DoesNotContain("ef_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COLLATE", sql);
    }

    [Fact]
    public void Decimal_division_is_real_division()
        => Assert.Contains("CAST(\"s\".\"Decimal\" AS REAL) / (\"s\".\"Decimal\" - 1.0)", Sql(s => s.Select(x => x.Decimal / (x.Decimal - 1m))));

    [Fact]
    public void Fractional_remainder_is_mod()
    {
        Assert.Contains("MOD(\"s\".\"Decimal\", 2.5)", Sql(s => s.Select(x => x.Decimal % 2.5m)));
        Assert.Contains("MOD(\"s\".\"Double\", 2.5)", Sql(s => s.Select(x => x.Double % 2.5)));
        Assert.Contains("\"s\".\"Long\" % 3", Sql(s => s.Select(x => x.Long % 3)));
    }
}

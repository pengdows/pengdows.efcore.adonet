using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Tests;

/// <summary>.NET members translated once in the core, written with the dialect's <see cref="FunctionSyntax" />.</summary>
public class TranslationTests
{
    private readonly FakeDatabase _db = new();

    private string Sql(IAdoNetDialect dialect, Func<ItemContext, IQueryable> query)
    {
        using var context = _db.Context(dialect);
        return query(context).ToQueryString().ReplaceLineEndings("\n");
    }

    private string Ansi(Func<ItemContext, IQueryable> query)
        => Sql(TestDialects.Ansi, query);

    [Fact]
    public void String_concatenation_uses_the_dialects_operator()
    {
        // EF keeps C#'s null + "x" == "x" with COALESCE.
        Assert.Contains(@"COALESCE(""i"".""Name"", '') || 'x'", Ansi(c => c.Items.Select(i => i.Name + "x")));
        Assert.Contains(@"CONCAT(COALESCE(""i"".""Name"", N''), N'x')", Sql(TestDialects.Functions, c => c.Items.Select(i => i.Name + "x")));
    }

    [Fact]
    public void String_literals_are_written_by_the_dialect()
    {
        Assert.Contains(@"= 'it''s\'", Ansi(c => c.Items.Where(i => i.Name == @"it's\")));
        Assert.Contains(@"= N'it''s\\'", Sql(TestDialects.Functions, c => c.Items.Where(i => i.Name == @"it's\")));
        Assert.Contains(@"= N'x'", Sql(TestDialects.Functions, c => c.Items.Where(i => i.Name!.Substring(1, 2) == "x")));
    }

    [Fact]
    public void Literals_of_every_type_are_formatted_by_the_dialect()
    {
        string Where(IAdoNetDialect dialect, Expression<Func<Item, bool>> predicate)
            => Sql(dialect, c => c.Items.Where(predicate)).Split("WHERE ")[1];

        Assert.Equal(@"""i"".""Small"" > 20", Where(TestDialects.Ansi, i => i.Small > 20));
        Assert.Equal(@"""i"".""Small"" > CAST(20 AS SMALLINT)", Where(TestDialects.CastLiterals, i => i.Small > 20));
        Assert.Equal(@"""i"".""Tiny"" > CAST(2 AS SMALLINT)", Where(TestDialects.CastLiterals, i => i.Tiny > 2));
        Assert.Equal(@"""i"".""Big"" > CAST(2 AS BIGINT)", Where(TestDialects.CastLiterals, i => i.Big > 2));
        Assert.Equal(@"""i"".""Double"" > 1.5E0", Where(TestDialects.CastLiterals, i => i.Double > 1.5));
        Assert.Equal(@"""i"".""Single"" > CAST(1.5 AS REAL)", Where(TestDialects.CastLiterals, i => i.Single > 1.5f));
        Assert.Equal(@"""i"".""Price"" > CAST(1.5 AS DECIMAL(18,2))", Where(TestDialects.CastLiterals, i => i.Price > 1.5m));
        Assert.Equal(@"""i"".""Version"" > CAST(2 AS INTEGER)", Where(TestDialects.CastLiterals, i => i.Version > 2));
        Assert.Equal(@"""i"".""At"" > '2020-01-02T03:04:05'", Where(TestDialects.CastLiterals, i => i.At > new DateTime(2020, 1, 2, 3, 4, 5)));
        Assert.Equal(@"""i"".""Day"" > {d '2020-01-02'}", Where(TestDialects.CastLiterals, i => i.Day > new DateOnly(2020, 1, 2)));
        Assert.Equal(@"""i"".""Time"" > {t '03:04:05'}", Where(TestDialects.CastLiterals, i => i.Time > new TimeOnly(3, 4, 5)));
        Assert.Equal(@"""i"".""Span"" > {t '03:04:05'}", Where(TestDialects.CastLiterals, i => i.Span > new TimeSpan(3, 4, 5)));
        Assert.Equal(@"""i"".""AtOffset"" > {ts '2020-01-02 03:04:05'}",
            Where(TestDialects.CastLiterals, i => i.AtOffset > new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        Assert.Equal(@"""i"".""Uid"" = {guid '00000000-0000-0000-0000-000000000001'}",
            Where(TestDialects.CastLiterals, i => i.Uid == new Guid("00000000-0000-0000-0000-000000000001")));
        Assert.Equal(@"""i"".""Data"" = 0x0102", Where(TestDialects.CastLiterals, i => i.Data == new byte[] { 1, 2 }));
    }

    [Fact]
    public void Case_and_trimming()
    {
        Assert.Contains(@"UPPER(""i"".""Name"")", Ansi(c => c.Items.Select(i => i.Name!.ToUpper())));
        Assert.Contains(@"LOWER(""i"".""Name"")", Ansi(c => c.Items.Select(i => i.Name!.ToLower())));
        Assert.Contains(@"TRIM(""i"".""Name"")", Ansi(c => c.Items.Select(i => i.Name!.Trim())));
        Assert.Contains(@"TRIM(LEADING FROM ""i"".""Name"")", Ansi(c => c.Items.Select(i => i.Name!.TrimStart())));
        Assert.Contains(@"TRIM(TRAILING FROM ""i"".""Name"")", Ansi(c => c.Items.Select(i => i.Name!.TrimEnd())));
    }

    [Fact]
    public void Length_substring_position_and_replace()
    {
        Assert.Contains(@"CHAR_LENGTH(""i"".""Name"")", Ansi(c => c.Items.Select(i => i.Name!.Length)));
        Assert.Contains(@"SUBSTRING(""i"".""Name"" FROM 2)", Ansi(c => c.Items.Select(i => i.Name!.Substring(1))));
        Assert.Contains(@"SUBSTRING(""i"".""Name"" FROM 2 FOR 3)", Ansi(c => c.Items.Select(i => i.Name!.Substring(1, 3))));
        Assert.Contains(@"POSITION('a' IN ""i"".""Name"") - 1", Ansi(c => c.Items.Select(i => i.Name!.IndexOf("a"))));
        Assert.Contains(@"REPLACE(""i"".""Name"", 'a', 'b')", Ansi(c => c.Items.Select(i => i.Name!.Replace("a", "b"))));
    }

    [Fact]
    public void Substring_from_a_column_adds_one_in_sql()
        => Assert.Contains(@"SUBSTRING(""i"".""Name"" FROM ""i"".""Version"" + 1)", Ansi(c => c.Items.Select(i => i.Name!.Substring(i.Version))));

    [Fact]
    public void Constant_patterns_are_like_patterns()
    {
        Assert.Contains(@"""i"".""Name"" LIKE 'ab%'", Ansi(c => c.Items.Where(i => i.Name!.StartsWith("ab"))));
        Assert.Contains(@"""i"".""Name"" LIKE '%ab'", Ansi(c => c.Items.Where(i => i.Name!.EndsWith("ab"))));
        Assert.Contains(@"""i"".""Name"" LIKE '%ab%'", Ansi(c => c.Items.Where(i => i.Name!.Contains("ab"))));
    }

    [Fact]
    public void Like_wildcards_in_constant_patterns_are_escaped()
    {
        Assert.Contains(@"LIKE '%a\%b\_c\\%' ESCAPE '\'", Ansi(c => c.Items.Where(i => i.Name!.Contains(@"a%b_c\"))));
        Assert.Contains(@"LIKE N'a![b%' ESCAPE N'!'", Sql(TestDialects.Functions, c => c.Items.Where(i => i.Name!.StartsWith("a[b"))));
    }

    [Fact]
    public void Patterns_from_parameters_and_columns_are_compared_not_matched()
    {
        var p = "ab";

        Assert.Matches(@"SUBSTRING\(""i"".""Name"" FROM 1 FOR CHAR_LENGTH\(@p\w*\)\) = @p", Ansi(c => c.Items.Where(i => i.Name!.StartsWith(p))));
        Assert.Matches(@"SUBSTRING\(""i"".""Name"" FROM \(CHAR_LENGTH\(""i"".""Name""\) - CHAR_LENGTH\(@p\w*\)\) \+ 1\) = @p", Ansi(c => c.Items.Where(i => i.Name!.EndsWith(p))));
        Assert.Matches(@"POSITION\(@p\w* IN ""i"".""Name""\) > 0", Ansi(c => c.Items.Where(i => i.Name!.Contains(p))));
        Assert.Contains(@"POSITION(""p"".""Label"" IN ""i"".""Name"") > 0", Ansi(c => c.Items.Where(i => i.Parts.Any(p => i.Name!.Contains(p.Label!)))));
    }

    [Fact]
    public void Null_or_white_space()
        => Assert.Contains(@"""i"".""Name"" IS NULL OR TRIM(""i"".""Name"") = ''", Ansi(c => c.Items.Where(i => string.IsNullOrWhiteSpace(i.Name))));

    [Fact]
    public void Math()
    {
        Assert.Contains(@"ABS(""i"".""Price"")", Ansi(c => c.Items.Select(i => System.Math.Abs(i.Price))));
        Assert.Contains(@"ABS(""i"".""Version"")", Ansi(c => c.Items.Select(i => System.Math.Abs(i.Version))));
        Assert.Contains(@"FLOOR(""i"".""Double"")", Ansi(c => c.Items.Select(i => System.Math.Floor(i.Double))));
        Assert.Contains(@"CEILING(""i"".""Price"")", Ansi(c => c.Items.Select(i => System.Math.Ceiling(i.Price))));
        Assert.Contains(@"ROUND(""i"".""Price"")", Ansi(c => c.Items.Select(i => System.Math.Round(i.Price))));
        Assert.Contains(@"ROUND(""i"".""Double"", 2)", Ansi(c => c.Items.Select(i => System.Math.Round(i.Double, 2))));
        Assert.Contains(@"POWER(""i"".""Double"", 2.0)", Ansi(c => c.Items.Select(i => System.Math.Pow(i.Double, 2))));
        Assert.Contains(@"SQRT(""i"".""Double"")", Ansi(c => c.Items.Select(i => System.Math.Sqrt(i.Double))));
    }

    [Fact]
    public void Date_parts()
    {
        Assert.Contains(@"EXTRACT(YEAR FROM ""i"".""At"")", Ansi(c => c.Items.Select(i => i.At.Year)));
        Assert.Contains(@"EXTRACT(MONTH FROM ""i"".""At"")", Ansi(c => c.Items.Select(i => i.At.Month)));
        Assert.Contains(@"EXTRACT(DAY FROM ""i"".""At"")", Ansi(c => c.Items.Select(i => i.At.Day)));
        Assert.Contains(@"EXTRACT(HOUR FROM ""i"".""At"")", Ansi(c => c.Items.Select(i => i.At.Hour)));
        Assert.Contains(@"EXTRACT(MINUTE FROM ""i"".""At"")", Ansi(c => c.Items.Select(i => i.At.Minute)));
        Assert.Contains(@"CAST(FLOOR(EXTRACT(SECOND FROM ""i"".""At"")) AS INTEGER)", Ansi(c => c.Items.Select(i => i.At.Second)));
        Assert.Contains(@"EXTRACT(YEAR FROM ""i"".""Day"")", Ansi(c => c.Items.Select(i => i.Day.Year)));
    }

    [Fact]
    public void Date_offset_parts_are_taken_from_the_value_as_the_dialect_writes_it()
    {
        Assert.Contains(@"EXTRACT(HOUR FROM ""i"".""AtOffset"")", Ansi(c => c.Items.Select(i => i.AtOffset.Hour)));
        Assert.Contains(@"DATEPART(year, LOCAL(""i"".""AtOffset""))", Sql(TestDialects.Functions, c => c.Items.Select(i => i.AtOffset.Year)));
        Assert.Contains(@"DATEPART(year, ""i"".""At"")", Sql(TestDialects.Functions, c => c.Items.Select(i => i.At.Year)));
    }

    [Fact]
    public void A_dialect_respells_a_function()
    {
        Assert.Contains(@"LEN(""i"".""Name"")", Sql(TestDialects.Functions, c => c.Items.Select(i => i.Name!.Length)));
        Assert.Contains(@"DATEPART(year, ""i"".""At"")", Sql(TestDialects.Functions, c => c.Items.Select(i => i.At.Year)));
        Assert.Contains(@"CHARINDEX(N'a', ""i"".""Name"") - 1", Sql(TestDialects.Functions, c => c.Items.Select(i => i.Name!.IndexOf("a"))));
    }

    [Fact]
    public void Templates_escape_braces()
        => Assert.Contains(@"{fn UCASE(""i"".""Name"")}", Sql(TestDialects.Functions, c => c.Items.Select(i => i.Name!.ToUpper())));

    [Fact]
    public void A_function_the_dialect_lacks_is_not_translated()
    {
        // In a final Select it would run on the client instead.
        var error = Assert.Throws<InvalidOperationException>(() => Sql(TestDialects.Functions, c => c.Items.Where(i => System.Math.Sqrt(i.Double) > 1)));

        Assert.Contains("could not be translated", error.Message);
    }

    public static TheoryData<string, Expression<Func<Item, bool>>> Untranslated => new()
    {
        { "Length", i => i.Name!.Length > 1 },
        { "Upper", i => i.Name!.ToUpper() == "A" },
        { "IndexOf", i => i.Name!.IndexOf("a") > 1 },
        { "StartsWith(parameter)", i => i.Name!.StartsWith(i.Name) },
        { "EndsWith(parameter)", i => i.Name!.EndsWith(i.Name) },
        { "Contains(parameter)", i => i.Name!.Contains(i.Name) },
        { "IsNullOrWhiteSpace", i => string.IsNullOrWhiteSpace(i.Name) },
        { "Abs", i => System.Math.Abs(i.Price) > 1 },
        { "Year", i => i.At.Year > 1 },
        { "Hour of an offset", i => i.AtOffset.Hour > 1 }
    };

    [Theory]
    [MemberData(nameof(Untranslated))]
    public void Nothing_is_translated_where_the_dialect_has_no_template(string _, Expression<Func<Item, bool>> predicate)
        => Assert.Contains("could not be translated", Assert.Throws<InvalidOperationException>(() => Sql(TestDialects.NoFunctions, c => c.Items.Where(predicate))).Message);

    public static TheoryData<string, Expression<Func<Item, bool>>> OtherOverloads => new()
    {
        { "Trim(char)", i => i.Name!.Trim('x') == "a" },
        { "IndexOf(char)", i => i.Name!.IndexOf('a') > 1 },
        { "Replace(char, char)", i => i.Name!.Replace('a', 'b') == "a" },
        { "Contains(char)", i => i.Name!.Contains('a') },
        { "PadLeft", i => i.Name!.PadLeft(3) == "a" },
        { "string.Join", i => string.Join(",", new[] { i.Name, i.Name }) == "a" },
        { "Round(MidpointRounding)", i => System.Math.Round(i.Double, MidpointRounding.ToEven) > 1 },
        { "Math.Log", i => System.Math.Log(i.Double) > 1 },
        { "DayOfYear", i => i.At.DayOfYear > 1 },
        { "TimeOnly.Hour", i => i.Time.Hour > 1 },
        { "Guid.ToByteArray", i => i.Uid.ToByteArray().Length > 1 }
    };

    [Theory]
    [MemberData(nameof(OtherOverloads))]
    public void Other_members_are_left_to_other_translators(string _, Expression<Func<Item, bool>> predicate)
        => Assert.Contains("could not be translated", Assert.Throws<InvalidOperationException>(() => Ansi(c => c.Items.Where(predicate))).Message);

    [Fact]
    public void Template_functions_run_and_read_their_results()
    {
        _db.Returns(FakeDatabase.Row("c", 4));
        using var context = _db.Context(TestDialects.Ansi);

        Assert.Equal([4], context.Items.Select(i => i.Name!.Length).ToList());
        Assert.Contains("CHAR_LENGTH", _db.Commands.Single());
    }

    [Fact]
    public void A_plugin_adds_translators_without_changing_the_core()
    {
        using var context = _db.Context(TestDialects.ReverseTranslator);

        Assert.Contains(@"REVERSE(""i"".""Name"")", context.Items.Select(i => PluginFunctions.Reverse(i.Name!)).ToQueryString());
    }
}

/// <summary>Spellings a non-ISO database would use, and one function it lacks.</summary>
public sealed class TestFunctions : FunctionSyntax
{
    public override string Concat => "CONCAT({0}, {1})";
    public override string Length => "LEN({0})";
    public override string Position => "CHARINDEX({0}, {1})";
    public override string Upper => "{{fn UCASE({0})}}";
    public override string Year => "DATEPART(year, {0})";
    public override string OffsetDateTime => "LOCAL({0})";
    public override string? Sqrt => null;
    public override char LikeEscapeCharacter => '!';
    public override string LikeSpecialCharacters => "%_[";
}

/// <summary>Formats every literal its own way, wrapping or replacing EF's.</summary>
public sealed class CastLiteralTypeMappings : AnsiTypeMappings
{
    public override string FormatLiteral(object value, string storeType, string literal)
        => value switch
        {
            double => literal + "E0",
            DateTime d => $"'{d:yyyy-MM-ddTHH:mm:ss}'",
            DateOnly d => $"{{d '{d:yyyy-MM-dd}'}}",
            TimeOnly t => $"{{t '{t:HH\\:mm\\:ss}'}}",
            TimeSpan t => $"{{t '{t:hh\\:mm\\:ss}'}}",
            DateTimeOffset d => $"{{ts '{d:yyyy-MM-dd HH:mm:ss}'}}",
            Guid g => $"{{guid '{g}'}}",
            byte[] b => "0x" + Convert.ToHexString(b),
            _ => $"CAST({literal} AS {storeType})"
        };
}

/// <summary>A database with none of the functions.</summary>
public sealed class NoFunctions : FunctionSyntax
{
    public override string? Upper => null;
    public override string? Length => null;
    public override string? Position => null;
    public override string? Trim => null;
    public override string? Abs => null;
    public override string? Year => null;
    public override string? OffsetDateTime => null;
}

public static class PluginFunctions
{
    public static string Reverse(string value)
        => throw new InvalidOperationException("Only in queries.");
}

/// <summary>Registered the way any EF provider plugin registers one: <see cref="IMethodCallTranslatorPlugin" />.</summary>
public sealed class ReverseTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory) : IMethodCallTranslatorPlugin, IMethodCallTranslator
{
    public IEnumerable<IMethodCallTranslator> Translators
        => [this];

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        => method.DeclaringType == typeof(PluginFunctions)
            ? sqlExpressionFactory.Function("REVERSE", arguments, nullable: true, [true], typeof(string), arguments[0].TypeMapping)
            : null;
}

using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.sqlite;

/// <summary>
///     SQLite's functions for LINQ queries, as <c>EF.Functions.X(...)</c>, as Microsoft's SQLite provider has them; each throws
///     when called outside a query. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Extensions/SqliteDbFunctionsExtensions.cs
/// </summary>
public static class SqliteDbFunctionsExtensions
{
    /// <summary>Whether <paramref name="matchExpression" /> matches the Unix file-name <paramref name="pattern" />, case-sensitively (GLOB).</summary>
    public static bool Glob(this DbFunctions _, string matchExpression, string pattern)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Glob)));

    /// <summary><paramref name="bytes" /> as upper-case hexadecimal text (HEX).</summary>
    public static string Hex(this DbFunctions _, byte[] bytes)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Hex)));

    /// <summary>The bytes the hexadecimal text <paramref name="value" /> spells, or null when it isn't hexadecimal (UNHEX, SQLite 3.41).</summary>
    public static byte[]? Unhex(this DbFunctions _, string value)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Unhex)));

    /// <summary>UNHEX, skipping any of <paramref name="ignoreChars" /> between pairs of digits.</summary>
    public static byte[]? Unhex(this DbFunctions _, string value, string ignoreChars)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Unhex)));

    /// <summary>The bytes of <paramref name="bytes" /> from the 1-based <paramref name="startIndex" /> (SUBSTR).</summary>
    public static byte[] Substr(this DbFunctions _, byte[] bytes, int startIndex)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Substr)));

    /// <summary><paramref name="length" /> bytes of <paramref name="bytes" /> from the 1-based <paramref name="startIndex" /> (SUBSTR).</summary>
    public static byte[] Substr(this DbFunctions _, byte[] bytes, int startIndex, int length)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Substr)));
}

internal sealed class SqliteMethodCallTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } = [new SqliteMethodTranslator(sqlExpressionFactory, typeMappingSource)];
}

internal sealed class SqliteMemberTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMemberTranslatorPlugin
{
    public IEnumerable<IMemberTranslator> Translators { get; } = [new SqliteMemberTranslator(sqlExpressionFactory, typeMappingSource)];
}

/// <summary>
///     What Microsoft's SQLite provider translates beyond the core's <see cref="FunctionSyntax" />: <see cref="SqliteDbFunctionsExtensions" />,
///     <c>EF.Functions.Random</c>, a string's first and last character and a character's case, a date's arithmetic kept a date,
///     ticks added to a date and time, and values written as text. Plugins run before the core's translators.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/tree/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/Translators
/// </summary>
internal sealed class SqliteMethodTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    // A date and time is text; the trailing zeros of its fraction, and a bare point, are trimmed so it reads as the text
    // Microsoft.Data.Sqlite writes for the same value.
    internal const string DateTimeText = "RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', {0}, {1}), '0'), '.')";
    private const string TimeText = "RTRIM(RTRIM(STRFTIME('%H:%M:%f', {0}, {1}), '0'), '.')";

    // Types Microsoft's provider writes as text with CAST, and an empty string for null, as .NET's ToString never returns null.
    private static readonly HashSet<Type> TextualTypes =
    [
        typeof(byte), typeof(char), typeof(DateOnly), typeof(DateTime), typeof(DateTimeOffset), typeof(decimal), typeof(double),
        typeof(float), typeof(Guid), typeof(int), typeof(long), typeof(sbyte), typeof(short), typeof(TimeOnly), typeof(TimeSpan),
        typeof(uint), typeof(ushort)
    ];

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        var type = method.DeclaringType;
        if (type == typeof(SqliteDbFunctionsExtensions))
        {
            return DbFunction(method, arguments);
        }

        if ((type == typeof(DbFunctionsExtensions)) && (method.Name == nameof(DbFunctionsExtensions.Random)) && (arguments.Count == 1))
        {
            // RANDOM() is a 64-bit integer; divided by about 2^63 it is a double from -1 to 1. Behaviour reference (not copied):
            // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/Translators/SqliteRandomTranslator.cs
            return sqlExpressionFactory.Call("ABS(RANDOM() / 9223372036854780000.0)", [], typeof(double), typeMappingSource.FindMapping(typeof(double)));
        }

        if ((type == typeof(char)) && (instance == null) && (arguments.Count == 1)
            && (method.Name is nameof(char.ToUpper) or nameof(char.ToLower)))
        {
            var template = method.Name == nameof(char.ToUpper) ? "UPPER({0})" : "LOWER({0})";
            return sqlExpressionFactory.Call(template, [sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!], typeof(char), arguments[0].TypeMapping);
        }

        if ((type == typeof(Enumerable)) && (instance == null) && (arguments is [{ Type: var source } text]) && (source == typeof(string)))
        {
            var template = method.Name switch
            {
                nameof(Enumerable.FirstOrDefault) => "SUBSTR({0}, 1, 1)",
                nameof(Enumerable.LastOrDefault) => "SUBSTR({0}, LENGTH({0}), 1)",
                _ => null
            };
            return sqlExpressionFactory.Call(template, [sqlExpressionFactory.ApplyDefaultTypeMapping(text)!], typeof(char), null);
        }

        if (instance == null)
        {
            return null;
        }

        if ((method.Name == nameof(ToString)) && (arguments.Count == 0))
        {
            return AsText(instance);
        }

        return type == typeof(DateOnly)
            ? DateArithmetic(instance, method, arguments)
            : type == typeof(DateTime)
                ? DateTimeArithmetic(instance, method, arguments)
                : type == typeof(TimeOnly)
                    ? TimeArithmetic(instance, method, arguments)
                    : null;
    }

    private SqlExpression? DbFunction(MethodInfo method, IReadOnlyList<SqlExpression> arguments)
    {
        var values = arguments.Skip(1).ToArray();
        switch (method.Name)
        {
            case nameof(SqliteDbFunctionsExtensions.Glob):
            {
                var mapping = ExpressionExtensions.InferTypeMapping(values) ?? typeMappingSource.FindMapping(typeof(string));
                return sqlExpressionFactory.Call(
                    "({0} GLOB {1})",
                    values.Select(v => sqlExpressionFactory.ApplyTypeMapping(v, mapping)!).ToArray(),
                    typeof(bool),
                    typeMappingSource.FindMapping(typeof(bool)));
            }
            case nameof(SqliteDbFunctionsExtensions.Hex):
                return sqlExpressionFactory.Call(
                    "HEX({0})", [sqlExpressionFactory.ApplyDefaultTypeMapping(values[0])!], typeof(string), typeMappingSource.FindMapping(typeof(string)));
            case nameof(SqliteDbFunctionsExtensions.Unhex):
                return sqlExpressionFactory.Call(
                    values.Length == 1 ? "UNHEX({0})" : "UNHEX({0}, {1})",
                    values.Select(v => sqlExpressionFactory.ApplyDefaultTypeMapping(v)!).ToArray(),
                    typeof(byte[]),
                    typeMappingSource.FindMapping(typeof(byte[])));
            case nameof(SqliteDbFunctionsExtensions.Substr):
            {
                var bytes = sqlExpressionFactory.ApplyDefaultTypeMapping(values[0])!;
                return sqlExpressionFactory.Call(
                    values.Length == 2 ? "SUBSTR({0}, {1})" : "SUBSTR({0}, {1}, {2})",
                    [bytes, .. values.Skip(1).Select(v => sqlExpressionFactory.ApplyDefaultTypeMapping(v)!)],
                    typeof(byte[]),
                    bytes.TypeMapping);
            }
            default:
                return null;
        }
    }

    // A bool is True or False, as .NET writes it; other values are their text. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/Translators/SqliteObjectToStringTranslator.cs
    private SqlExpression? AsText(SqlExpression instance)
    {
        if (instance.TypeMapping?.Converter != null)
        {
            return null;
        }

        var stringMapping = typeMappingSource.FindMapping(typeof(string));
        if (instance.Type == typeof(bool))
        {
            return sqlExpressionFactory.Call("CASE {0} WHEN 0 THEN 'False' WHEN 1 THEN 'True' ELSE '' END", [instance], typeof(string), stringMapping);
        }

        return TextualTypes.Contains(instance.Type)
            ? sqlExpressionFactory.Call("COALESCE(CAST({0} AS TEXT), '')", [instance], typeof(string), stringMapping)
            : null;
    }

    // A date plus years, months or days, as SQLite's DATE, so it stays the text of a date. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/Translators/SqliteDateTimeMethodTranslator.cs
    private SqlExpression? DateArithmetic(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments)
    {
        var unit = method.Name switch
        {
            nameof(DateOnly.AddYears) => "years",
            nameof(DateOnly.AddMonths) => "months",
            nameof(DateOnly.AddDays) => "days",
            _ => null
        };
        return (unit == null) || (arguments.Count != 1)
            ? null
            : sqlExpressionFactory.Call(
                "DATE({0}, CAST({1} AS TEXT) || ' " + unit + "')",
                [instance, sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!],
                instance.Type,
                instance.TypeMapping);
    }

    // Ticks, which the core has no unit for: tenths of a microsecond, added as seconds.
    private SqlExpression? DateTimeArithmetic(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments)
        => (method.Name == nameof(DateTime.AddTicks)) && (arguments.Count == 1)
            ? Plus(DateTimeText, instance, Seconds(arguments[0], TimeSpan.TicksPerSecond))
            : null;

    // A time of day plus hours or minutes, wrapping past midnight as TimeOnly does; the core's date and time would add a date.
    private SqlExpression? TimeArithmetic(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments)
    {
        var secondsPer = method.Name switch
        {
            nameof(TimeOnly.AddHours) => 3600.0,
            nameof(TimeOnly.AddMinutes) => 60.0,
            _ => (double?)null
        };
        if ((secondsPer is not { } factor) || (arguments.Count != 1))
        {
            return null;
        }

        var amount = sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!;
        return Plus(TimeText, instance, Modifier(sqlExpressionFactory.Multiply(amount, sqlExpressionFactory.Constant(factor, amount.TypeMapping))));
    }

    private SqlExpression Seconds(SqlExpression amount, double divisor)
    {
        amount = sqlExpressionFactory.ApplyDefaultTypeMapping(amount)!;
        return Modifier(sqlExpressionFactory.Divide(amount, sqlExpressionFactory.Constant(divisor, typeMappingSource.FindMapping(typeof(double)))));
    }

    // An STRFTIME modifier: '<n> seconds'.
    private SqlExpression Modifier(SqlExpression seconds)
        => sqlExpressionFactory.Call("CAST({0} AS TEXT) || ' seconds'", [seconds], typeof(string), typeMappingSource.FindMapping(typeof(string)))!;

    private SqlExpression? Plus(string template, SqlExpression instance, SqlExpression modifier)
        => sqlExpressionFactory.Call(template, [instance, modifier], instance.Type, instance.TypeMapping);
}

/// <summary>
///     <see cref="DateTime.Ticks" />, from the Julian day of 0001-01-01. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/Translators/SqliteDateTimeMemberTranslator.cs
/// </summary>
internal sealed class SqliteMemberTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMemberTranslator
{
    public SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        => (instance != null) && (member.DeclaringType == typeof(DateTime)) && (member.Name == nameof(DateTime.Ticks))
            ? sqlExpressionFactory.Call(
                "CAST((JULIANDAY({0}) - 1721425.5) * 864000000000.0 AS INTEGER)", [instance], typeof(long), typeMappingSource.FindMapping(typeof(long)))
            : null;
}

/// <summary>
///     SQLite's <c>X REGEXP Y</c> calls a function named regexp that the connection supplies; each Microsoft.Data.Sqlite
///     connection the context opens gets .NET's <see cref="Regex.IsMatch(string, string)" />, as Microsoft's provider gives it. A
///     connection opened before the context sees it, or of another driver, has none, and such a query fails in the database.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Storage/Internal/SqliteRelationalConnection.cs
/// </summary>
internal sealed class SqliteConnectionFunctionsInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => AddFunctions(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        AddFunctions(connection);
        return Task.CompletedTask;
    }

    private static void AddFunctions(DbConnection connection)
    {
        if (connection is not SqliteConnection sqlite)
        {
            return;
        }

        sqlite.CreateFunction<string?, string?, bool?>(
            "regexp",
            (pattern, input) => (pattern == null) || (input == null) ? null : Regex.IsMatch(input, pattern),
            isDeterministic: true);
    }
}

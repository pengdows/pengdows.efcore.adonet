using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL's functions in queries, as Pomelo's MySqlDbFunctionsExtensions has them: CONVERT_TZ, LIKE over any value, HEX,
///     UNHEX, DEGREES and RADIANS. Full-text MATCH is <see cref="MySqlFullTextExtensions" />'s, and DateDiff* is the core's.
///     Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlDbFunctionsExtensions.cs
/// </summary>
public static class MySqlDbFunctionsExtensions
{
    /// <summary><paramref name="dateTime" /> moved from one time zone to another: CONVERT_TZ; null for a zone the server doesn't know.</summary>
    public static DateTime? ConvertTimeZone(this DbFunctions _, DateTime dateTime, string fromTimeZone, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateOnly" /> moved from one time zone to another: CONVERT_TZ; null for a zone the server doesn't know.</summary>
    public static DateOnly? ConvertTimeZone(this DbFunctions _, DateOnly dateOnly, string fromTimeZone, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateTime" /> moved from one time zone to another: CONVERT_TZ; null for a zone the server doesn't know.</summary>
    public static DateTime? ConvertTimeZone(this DbFunctions _, DateTime? dateTime, string fromTimeZone, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateOnly" /> moved from one time zone to another: CONVERT_TZ; null for a zone the server doesn't know.</summary>
    public static DateOnly? ConvertTimeZone(this DbFunctions _, DateOnly? dateOnly, string fromTimeZone, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateTime" />, in the session's time zone, moved to <paramref name="toTimeZone" />: CONVERT_TZ.</summary>
    public static DateTime? ConvertTimeZone(this DbFunctions _, DateTime dateTime, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateTimeOffset" />'s UTC instant moved to <paramref name="toTimeZone" />: CONVERT_TZ.</summary>
    public static DateTime? ConvertTimeZone(this DbFunctions _, DateTimeOffset dateTimeOffset, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateOnly" />, in the session's time zone, moved to <paramref name="toTimeZone" />: CONVERT_TZ.</summary>
    public static DateOnly? ConvertTimeZone(this DbFunctions _, DateOnly dateOnly, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateTime" />, in the session's time zone, moved to <paramref name="toTimeZone" />: CONVERT_TZ.</summary>
    public static DateTime? ConvertTimeZone(this DbFunctions _, DateTime? dateTime, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateTimeOffset" />'s UTC instant moved to <paramref name="toTimeZone" />: CONVERT_TZ.</summary>
    public static DateTime? ConvertTimeZone(this DbFunctions _, DateTimeOffset? dateTimeOffset, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary><paramref name="dateOnly" />, in the session's time zone, moved to <paramref name="toTimeZone" />: CONVERT_TZ.</summary>
    public static DateOnly? ConvertTimeZone(this DbFunctions _, DateOnly? dateOnly, string toTimeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvertTimeZone)));

    /// <summary>SQL's LIKE over a value of any type, which MySQL compares as text.</summary>
    public static bool Like<T>(this DbFunctions _, T matchExpression, string pattern)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Like)));

    /// <summary>
    ///     SQL's LIKE over a value of any type, which MySQL compares as text; <paramref name="escapeCharacter" /> escapes
    ///     <c>%</c>, <c>_</c> and itself.
    /// </summary>
    public static bool Like<T>(this DbFunctions _, T matchExpression, string pattern, string escapeCharacter)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Like)));

    /// <summary>The hexadecimal digits of a string's bytes or of a number: HEX(value).</summary>
    public static string Hex<T>(this DbFunctions _, T value)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Hex)));

    /// <summary>The bytes the hexadecimal digits of <paramref name="value" /> stand for, as UTF-8 text; null where they aren't hexadecimal.</summary>
    public static string Unhex(this DbFunctions _, string value)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Unhex)));

    /// <summary>Radians in degrees: DEGREES(radians).</summary>
    public static double Degrees(this DbFunctions _, double radians)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Degrees)));

    /// <summary>Radians in degrees: DEGREES(radians).</summary>
    public static float Degrees(this DbFunctions _, float radians)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Degrees)));

    /// <summary>Degrees in radians: RADIANS(degrees).</summary>
    public static double Radians(this DbFunctions _, double degrees)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Radians)));

    /// <summary>Degrees in radians: RADIANS(degrees).</summary>
    public static float Radians(this DbFunctions _, float degrees)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Radians)));
}

internal sealed class MySqlDbFunctionsTranslatorPlugin(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect dialect) : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } =
        [new MySqlDbFunctionsTranslator(sqlExpressionFactory, typeMappingSource, dialect.Functions)];
}

/// <summary>
///     <see cref="MySqlDbFunctionsExtensions" />, which MariaDB shares. CONVERT_TZ and UNHEX are null for a zone or text they
///     can't read, so their nullability is not only their arguments'. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionTranslators/Internal/MySqlDbFunctionsExtensionsMethodTranslator.cs
/// </summary>
internal sealed class MySqlDbFunctionsTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    FunctionSyntax functions) : IMethodCallTranslator
{
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(MySqlDbFunctionsExtensions))
        {
            return null;
        }

        var args = arguments.Skip(1).ToArray();
        return method.Name switch
        {
            nameof(MySqlDbFunctionsExtensions.ConvertTimeZone) => ConvertTimeZone(method, args),
            nameof(MySqlDbFunctionsExtensions.Like) => Like(args),
            nameof(MySqlDbFunctionsExtensions.Hex) => sqlExpressionFactory.Call("HEX({0})", Default(args), typeof(string), Mapping(typeof(string))),
            // UNHEX returns bytes, which the drivers read only as bytes: Unhex returns them as text.
            nameof(MySqlDbFunctionsExtensions.Unhex) => sqlExpressionFactory.CallMaybeNull(
                "CONVERT(UNHEX({0}) USING utf8mb4)", Default(args), typeof(string), Mapping(typeof(string))),
            nameof(MySqlDbFunctionsExtensions.Degrees) => sqlExpressionFactory.Call(functions.Degrees, Default(args), method.ReturnType, Mapping(method.ReturnType)),
            nameof(MySqlDbFunctionsExtensions.Radians) => sqlExpressionFactory.Call(functions.Radians, Default(args), method.ReturnType, Mapping(method.ReturnType)),
            _ => null
        };
    }

    private RelationalTypeMapping? Mapping(Type type)
        => typeMappingSource.FindMapping(Nullable.GetUnderlyingType(type) ?? type);

    private SqlExpression[] Default(IEnumerable<SqlExpression> arguments)
        => arguments.Select(a => sqlExpressionFactory.ApplyDefaultTypeMapping(a)).ToArray();

    // Without a zone to convert from, a DateTimeOffset is UTC (it is stored as its UTC instant) and anything else is in the
    // session's zone.
    private SqlExpression ConvertTimeZone(MethodInfo method, SqlExpression[] args)
    {
        var from = method.GetParameters()[1].ParameterType;
        var template = args.Length == 3 ? "CONVERT_TZ({0}, {1}, {2})"
            : (Nullable.GetUnderlyingType(from) ?? from) == typeof(DateTimeOffset) ? "CONVERT_TZ({0}, '+00:00', {1})"
            : "CONVERT_TZ({0}, @@session.time_zone, {1})";
        return sqlExpressionFactory.CallMaybeNull(template, Default(args), method.ReturnType, Mapping(method.ReturnType));
    }

    // A text pattern takes the value's mapping when the value is text, as EF's own Like does.
    private SqlExpression Like(SqlExpression[] args)
    {
        var match = sqlExpressionFactory.ApplyDefaultTypeMapping(args[0]);
        var textMapping = match.TypeMapping?.ClrType == typeof(string) ? match.TypeMapping : null;
        var pattern = Text(args[1], textMapping);
        var escape = args.Length == 3 ? Text(args[2], textMapping) : null;
        return sqlExpressionFactory.Like(match, pattern, escape);
    }

    private SqlExpression Text(SqlExpression expression, RelationalTypeMapping? mapping)
        => (expression.TypeMapping == null) && (mapping != null)
            ? sqlExpressionFactory.ApplyTypeMapping(expression, mapping)
            : sqlExpressionFactory.ApplyDefaultTypeMapping(expression);
}

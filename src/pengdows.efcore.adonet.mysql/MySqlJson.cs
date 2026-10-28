using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL's JSON functions, as Pomelo has them. A <c>json</c> argument is a JSON column, a string holding JSON, or JSON
///     text; MySQL parses a string argument as JSON. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlJsonDbFunctionsExtensions.cs
/// </summary>
public static class MySqlJsonDbFunctionsExtensions
{
    /// <summary>
    ///     <paramref name="value" /> as JSON rather than as a string: <c>CAST(value AS JSON)</c>; on MariaDB, whose JSON is text,
    ///     <c>JSON_COMPACT(value)</c>. Outside a query, the value itself.
    /// </summary>
    public static string AsJson(this DbFunctions _, string value)
        => value;

    /// <summary>The type of the outermost JSON value: JSON_TYPE(json), such as <c>OBJECT</c>, <c>ARRAY</c> or <c>INTEGER</c>.</summary>
    public static string JsonType(this DbFunctions _, object json)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonType)));

    /// <summary><paramref name="value" /> as a JSON string, quoted and escaped: JSON_QUOTE(value).</summary>
    public static string JsonQuote(this DbFunctions _, string value)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonQuote)));

    /// <summary>A JSON value without its quotes, as text: JSON_UNQUOTE(json).</summary>
    public static string JsonUnquote(this DbFunctions _, object json)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonUnquote)));

    /// <summary>
    ///     The value at <paramref name="paths" /> in <paramref name="json" />: JSON_EXTRACT(json, path, ...). A string is the
    ///     JSON text; an object or collection is that text deserialized; any other type is the scalar at one path, read as its
    ///     type. Several paths give an array of their values.
    /// </summary>
    public static T JsonExtract<T>(this DbFunctions _, object json, params string[] paths)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonExtract)));

    /// <summary>Whether two JSON documents share a member, an element or a value: JSON_OVERLAPS(json1, json2).</summary>
    public static bool JsonOverlaps(this DbFunctions _, object json1, object json2)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonOverlaps)));

    /// <summary>Whether <paramref name="json" /> contains the JSON <paramref name="candidate" />: JSON_CONTAINS(json, candidate).</summary>
    public static bool JsonContains(this DbFunctions _, object json, object candidate)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonContains)));

    /// <summary>
    ///     Whether <paramref name="json" /> contains the JSON <paramref name="candidate" /> at <paramref name="path" />:
    ///     JSON_CONTAINS(json, candidate, path).
    /// </summary>
    public static bool JsonContains(this DbFunctions _, object json, object candidate, string? path)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonContains)));

    /// <summary>Whether <paramref name="path" /> exists in <paramref name="json" />: JSON_CONTAINS_PATH(json, 'one', path).</summary>
    public static bool JsonContainsPath(this DbFunctions _, object json, string path)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonContainsPath)));

    /// <summary>Whether any of <paramref name="paths" /> exists in <paramref name="json" />: JSON_CONTAINS_PATH(json, 'one', ...).</summary>
    public static bool JsonContainsPathAny(this DbFunctions _, object json, params string[] paths)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonContainsPathAny)));

    /// <summary>Whether every one of <paramref name="paths" /> exists in <paramref name="json" />: JSON_CONTAINS_PATH(json, 'all', ...).</summary>
    public static bool JsonContainsPathAll(this DbFunctions _, object json, params string[] paths)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonContainsPathAll)));

    /// <summary>
    ///     Whether a string in <paramref name="json" /> matches the LIKE pattern <paramref name="searchString" />:
    ///     JSON_SEARCH(json, 'one', searchString) IS NOT NULL.
    /// </summary>
    public static bool JsonSearchAny(this DbFunctions _, object json, string searchString)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonSearchAny)));

    /// <summary>
    ///     Whether a string under <paramref name="path" /> in <paramref name="json" /> matches the LIKE pattern
    ///     <paramref name="searchString" />: JSON_SEARCH(json, 'one', searchString, NULL, path) IS NOT NULL.
    /// </summary>
    public static bool JsonSearchAny(this DbFunctions _, object json, string searchString, string path)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonSearchAny)));

    /// <summary>
    ///     Whether a string under <paramref name="path" /> in <paramref name="json" /> matches the LIKE pattern
    ///     <paramref name="searchString" />, where <paramref name="escapeChar" /> (one character, empty or null for a backslash)
    ///     escapes <c>%</c> and <c>_</c>: JSON_SEARCH(json, 'one', searchString, escapeChar, path) IS NOT NULL.
    /// </summary>
    public static bool JsonSearchAny(this DbFunctions _, object json, string searchString, string path, string? escapeChar)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonSearchAny)));
}

internal sealed class MySqlJsonTranslatorPlugin(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect dialect,
    ITypeMappingDialect typeMappings) : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } =
        [new MySqlJsonTranslator(sqlExpressionFactory, typeMappingSource, dialect, typeMappings)];
}

/// <summary>
///     <see cref="MySqlJsonDbFunctionsExtensions" />: each is the MySQL function of its name, which MariaDB shares; only AsJson
///     differs, through the dialect's way of making text JSON. Paths given as a <c>params</c> array must be constants.
///     Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionTranslators/Internal/MySqlJsonDbFunctionsTranslator.cs
/// </summary>
internal sealed class MySqlJsonTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect dialect,
    ITypeMappingDialect typeMappings) : IMethodCallTranslator
{
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((method.DeclaringType != typeof(MySqlJsonDbFunctionsExtensions)) || (dialect.Json is not { } json))
        {
            return null;
        }

        var args = arguments.Skip(1).Select(a => sqlExpressionFactory.ApplyDefaultTypeMapping(RemoveConvert(a))).ToArray();
        return method.Name switch
        {
            nameof(MySqlJsonDbFunctionsExtensions.AsJson) => sqlExpressionFactory.Call(json.FromText, args, typeof(string), TextMapping),
            nameof(MySqlJsonDbFunctionsExtensions.JsonType) => sqlExpressionFactory.Call("JSON_TYPE({0})", args, typeof(string), TextMapping),
            nameof(MySqlJsonDbFunctionsExtensions.JsonQuote) => sqlExpressionFactory.Call("JSON_QUOTE({0})", args, typeof(string), TextMapping),
            nameof(MySqlJsonDbFunctionsExtensions.JsonUnquote) => sqlExpressionFactory.Call("JSON_UNQUOTE({0})", args, typeof(string), TextMapping),
            nameof(MySqlJsonDbFunctionsExtensions.JsonExtract) => Extract(json, method.ReturnType, args[0], Paths(args[1])),
            nameof(MySqlJsonDbFunctionsExtensions.JsonOverlaps) => Predicate("JSON_OVERLAPS({0}, {1})", args),
            nameof(MySqlJsonDbFunctionsExtensions.JsonContains) => Predicate(args.Length == 3 ? "JSON_CONTAINS({0}, {1}, {2})" : "JSON_CONTAINS({0}, {1})", args),
            nameof(MySqlJsonDbFunctionsExtensions.JsonContainsPath) => Predicate("JSON_CONTAINS_PATH({0}, 'one', {1})", args),
            nameof(MySqlJsonDbFunctionsExtensions.JsonContainsPathAny) => ContainsPath("one", args[0], Paths(args[1])),
            nameof(MySqlJsonDbFunctionsExtensions.JsonContainsPathAll) => ContainsPath("all", args[0], Paths(args[1])),
            nameof(MySqlJsonDbFunctionsExtensions.JsonSearchAny) => SearchAny(args),
            _ => null
        };
    }

    private RelationalTypeMapping TextMapping
        => typeMappingSource.FindMapping(typeof(string))!;

    private SqlExpression? Predicate(string template, IReadOnlyList<SqlExpression> arguments)
        => sqlExpressionFactory.Call(template, arguments, typeof(bool), typeMappingSource.FindMapping(typeof(bool)));

    private SqlExpression? ContainsPath(string oneOrAll, SqlExpression json, IReadOnlyList<SqlExpression>? paths)
        => paths == null
            ? null
            : Predicate("JSON_CONTAINS_PATH({0}, '" + oneOrAll + "'" + Placeholders(1, paths.Count) + ")", [json, .. paths]);

    // JSON_SEARCH is null where nothing matches, so its nullability is not only its arguments'. A null escape is a backslash.
    private SqlExpression SearchAny(IReadOnlyList<SqlExpression> args)
    {
        var template = args.Count switch
        {
            2 => "JSON_SEARCH({0}, 'one', {1})",
            3 => "JSON_SEARCH({0}, 'one', {1}, NULL, {2})",
            _ => "JSON_SEARCH({0}, 'one', {1}, {3}, {2})"
        };
        return sqlExpressionFactory.IsNotNull(MaybeNull(template, args, typeof(string), TextMapping));
    }

    // A string is the JSON text; a scalar at one path is read as its type, through the dialect's JSON value templates, so a
    // comparison compares values; an object or collection is the JSON text, deserialized when read. A scalar from several
    // paths would be an array of them, and is left untranslated.
    private SqlExpression? Extract(JsonSyntax json, Type type, SqlExpression document, IReadOnlyList<SqlExpression>? paths)
    {
        if (paths == null)
        {
            return null;
        }

        var extract = "JSON_EXTRACT({0}" + Placeholders(1, paths.Count) + ")";
        if (type == typeof(string))
        {
            return MaybeNull(extract, [document, .. paths], type, TextMapping);
        }

        var clrType = Nullable.GetUnderlyingType(type) ?? type;
        var mapping = typeMappingSource.FindMapping(type);
        if ((mapping == null) || (mapping.ElementTypeMapping != null))
        {
            var converter = (ValueConverter)Activator.CreateInstance(typeof(JsonTextConverter<>).MakeGenericType(type))!;
            return MaybeNull(extract, [document, .. paths], type, (RelationalTypeMapping)TextMapping.WithComposedConverter(converter));
        }

        if (paths.Count != 1)
        {
            return null;
        }

        var template = (clrType == typeof(byte[])) && (json.BinaryValue != null) ? json.BinaryValue
            : (clrType == typeof(bool)) && (json.BooleanValue != null) ? json.BooleanValue
            : json.TypedValue.Replace("{2}", typeMappings.CastTarget(mapping.StoreType), StringComparison.Ordinal);
        return MaybeNull(template, [document, paths[0]], type, mapping);
    }

    // A function that may be null even when no argument is: EF must not reduce its IS NULL to its arguments'.
    private SqlExpression MaybeNull(string template, IReadOnlyList<SqlExpression> arguments, Type type, RelationalTypeMapping mapping)
    {
        var call = (SqlFunctionExpression)sqlExpressionFactory.Call(template, arguments, type, mapping)!;
        return sqlExpressionFactory.Function(call.Name, call.Arguments!, nullable: true, call.Arguments!.Select(_ => false), type, mapping);
    }

    // ", {first}, {first + 1}, ..." for count arguments.
    private static string Placeholders(int first, int count)
        => string.Concat(Enumerable.Range(first, count).Select(i => ", {" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"));

    // A params array of paths is a constant; each path is written as a literal.
    private IReadOnlyList<SqlExpression>? Paths(SqlExpression paths)
        => paths is SqlConstantExpression { Value: string[] values }
            ? values.Select(v => (SqlExpression)sqlExpressionFactory.Constant(v, TextMapping)).ToArray()
            : null;

    // A JSON argument is an object, so a column or value comes wrapped in a conversion to object.
    private static SqlExpression RemoveConvert(SqlExpression expression)
    {
        while (expression is SqlUnaryExpression { OperatorType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }
}

/// <summary>A value read as the JSON text JSON_EXTRACT returns, deserialized.</summary>
internal sealed class JsonTextConverter<T>() : ValueConverter<T, string>(
    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
    text => JsonSerializer.Deserialize<T>(text, (JsonSerializerOptions?)null)!);

/// <summary>
///     Keeps MySQL's <c>EF.Functions</c> in the query even when every argument is a constant: AsJson returns its argument
///     outside a query, and would otherwise become a parameter instead of JSON. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/Internal/MySqlEvaluatableExpressionFilter.cs
/// </summary>
internal sealed class MySqlEvaluatableExpressionFilterPlugin : IEvaluatableExpressionFilterPlugin
{
    private static readonly Type[] FunctionTypes = [typeof(MySqlJsonDbFunctionsExtensions), typeof(MySqlFullTextExtensions)];

    public bool IsEvaluatableExpression(Expression expression)
        => (expression is not MethodCallExpression call) || !FunctionTypes.Contains(call.Method.DeclaringType);
}

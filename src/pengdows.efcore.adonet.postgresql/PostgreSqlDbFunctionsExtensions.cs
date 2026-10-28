using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     PostgreSQL's functions for LINQ queries, as <c>EF.Functions.X(...)</c>, as Npgsql's provider has them; each throws when
///     called outside a query. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlDbFunctionsExtensions.cs
/// </summary>
public static class PostgreSqlDbFunctionsExtensions
{
    /// <summary>Whether <paramref name="matchExpression" /> matches <paramref name="pattern" />, without regard to case (ILIKE).</summary>
    public static bool ILike(this DbFunctions _, string matchExpression, string pattern)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ILike)));

    /// <summary>ILIKE with <paramref name="escapeCharacter" /> escaping a wildcard in <paramref name="pattern" />.</summary>
    public static bool ILike(this DbFunctions _, string matchExpression, string pattern, string escapeCharacter)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ILike)));

    /// <summary>Whether the arrays have an element in common (<c>&amp;&amp;</c>).</summary>
    public static bool ArrayOverlaps<T>(this DbFunctions _, IEnumerable<T> array, IEnumerable<T> other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ArrayOverlaps)));

    /// <summary>Whether <paramref name="array" /> holds every element of <paramref name="other" /> (<c>@&gt;</c>).</summary>
    public static bool ArrayContains<T>(this DbFunctions _, IEnumerable<T> array, IEnumerable<T> other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ArrayContains)));

    /// <summary>Whether every element of <paramref name="array" /> is in <paramref name="other" /> (<c>&lt;@</c>).</summary>
    public static bool ArrayContainedBy<T>(this DbFunctions _, IEnumerable<T> array, IEnumerable<T> other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ArrayContainedBy)));
}

/// <summary>Translates <see cref="PostgreSqlDbFunctionsExtensions" />.</summary>
internal sealed class PostgreSqlDbFunctionsTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } =
    [
        new PostgreSqlDbFunctionsTranslator(sqlExpressionFactory, typeMappingSource),
        new PostgreSqlJsonDbFunctionsTranslator(sqlExpressionFactory, typeMappingSource)
    ];
}

internal sealed class PostgreSqlDbFunctionsTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    // Npgsql writes these operators for Any and All over arrays; here they are also functions. Behaviour reference (not copied):
    // https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Internal/NpgsqlQueryableMethodTranslatingExpressionVisitor.cs
    private static readonly Dictionary<string, string> ArrayOperators = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlDbFunctionsExtensions.ArrayOverlaps)] = "&&",
        [nameof(PostgreSqlDbFunctionsExtensions.ArrayContains)] = "@>",
        [nameof(PostgreSqlDbFunctionsExtensions.ArrayContainedBy)] = "<@"
    };

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(PostgreSqlDbFunctionsExtensions))
        {
            return null;
        }

        if (ArrayOperators.TryGetValue(method.Name, out var arrayOperator))
        {
            var arrayMapping = ExpressionExtensions.InferTypeMapping(arguments[1], arguments[2]);
            return PostgreSqlArraySql.Binary(
                sqlExpressionFactory,
                arrayOperator,
                sqlExpressionFactory.ApplyTypeMapping(arguments[1], arrayMapping),
                sqlExpressionFactory.ApplyTypeMapping(arguments[2], arrayMapping),
                typeof(bool),
                typeMappingSource.FindMapping(typeof(bool)));
        }

        if (method.Name != nameof(PostgreSqlDbFunctionsExtensions.ILike))
        {
            return null;
        }

        var mapping = ExpressionExtensions.InferTypeMapping(arguments[1], arguments[2]) ?? typeMappingSource.FindMapping(typeof(string));
        var operands = arguments.Skip(1).Select(a => sqlExpressionFactory.ApplyTypeMapping(a, mapping)!).ToArray();
        return sqlExpressionFactory.Call(
            operands.Length == 2 ? "{0} ILIKE {1}" : "{0} ILIKE {1} ESCAPE {2}",
            operands,
            typeof(bool),
            typeMappingSource.FindMapping(typeof(bool)));
    }
}

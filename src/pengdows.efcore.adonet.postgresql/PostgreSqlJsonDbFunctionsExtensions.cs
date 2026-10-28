using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     PostgreSQL's jsonb functions for LINQ queries, as Npgsql's <c>NpgsqlJsonDbFunctionsExtensions</c> has them. A JSON argument
///     is a jsonb column, or text (a string column, constant or parameter), which is cast to jsonb. Each throws when called
///     outside a query. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlJsonDbFunctionsExtensions.cs
/// </summary>
public static class PostgreSqlJsonDbFunctionsExtensions
{
    /// <summary>Whether <paramref name="json" /> contains <paramref name="contained" /> at its top level (<c>@&gt;</c>).</summary>
    public static bool JsonContains(this DbFunctions _, object json, object contained)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonContains)));

    /// <summary>Whether <paramref name="contained" /> is contained in <paramref name="json" /> at its top level (<c>&lt;@</c>).</summary>
    public static bool JsonContained(this DbFunctions _, object contained, object json)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonContained)));

    /// <summary>Whether <paramref name="key" /> is a top-level key of the object, or a string element of the array (<c>?</c>).</summary>
    public static bool JsonExists(this DbFunctions _, object json, string key)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonExists)));

    /// <summary>Whether any of <paramref name="keys" /> is a top-level key or string element (<c>?|</c>).</summary>
    public static bool JsonExistAny(this DbFunctions _, object json, params string[] keys)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonExistAny)));

    /// <summary>Whether every one of <paramref name="keys" /> is a top-level key or string element (<c>?&amp;</c>).</summary>
    public static bool JsonExistAll(this DbFunctions _, object json, params string[] keys)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonExistAll)));

    /// <summary>The type of the outermost JSON value: object, array, string, number, boolean or null (<c>jsonb_typeof</c>).</summary>
    public static string JsonTypeof(this DbFunctions _, object json)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(JsonTypeof)));
}

/// <summary>
///     Translates <see cref="PostgreSqlJsonDbFunctionsExtensions" />. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlJsonDbFunctionsTranslator.cs
/// </summary>
internal sealed class PostgreSqlJsonDbFunctionsTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    private const string Jsonb = "jsonb";

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(PostgreSqlJsonDbFunctionsExtensions))
        {
            return null;
        }

        var json = AsJsonb(arguments[1]);
        var boolMapping = typeMappingSource.FindMapping(typeof(bool));
        switch (method.Name)
        {
            case nameof(PostgreSqlJsonDbFunctionsExtensions.JsonTypeof):
            {
                return sqlExpressionFactory.Function(
                    "jsonb_typeof", [json], nullable: true, [true], typeof(string), typeMappingSource.FindMapping(typeof(string)));
            }

            case nameof(PostgreSqlJsonDbFunctionsExtensions.JsonContains):
            {
                return PostgreSqlArraySql.Binary(sqlExpressionFactory, "@>", json, AsJsonb(arguments[2]), typeof(bool), boolMapping);
            }

            case nameof(PostgreSqlJsonDbFunctionsExtensions.JsonContained):
            {
                return PostgreSqlArraySql.Binary(sqlExpressionFactory, "<@", json, AsJsonb(arguments[2]), typeof(bool), boolMapping);
            }

            case nameof(PostgreSqlJsonDbFunctionsExtensions.JsonExists):
            {
                var key = sqlExpressionFactory.ApplyTypeMapping(Unwrap(arguments[2]), typeMappingSource.FindMapping(typeof(string)));
                return PostgreSqlArraySql.Binary(sqlExpressionFactory, "?", json, key, typeof(bool), boolMapping);
            }

            case nameof(PostgreSqlJsonDbFunctionsExtensions.JsonExistAny):
            case nameof(PostgreSqlJsonDbFunctionsExtensions.JsonExistAll):
            {
                var keys = sqlExpressionFactory.ApplyTypeMapping(Unwrap(arguments[2]), typeMappingSource.FindMapping(typeof(string[]), "text[]"));
                var @operator = method.Name == nameof(PostgreSqlJsonDbFunctionsExtensions.JsonExistAny) ? "?|" : "?&";
                return PostgreSqlArraySql.Binary(sqlExpressionFactory, @operator, json, keys, typeof(bool), boolMapping);
            }

            default:
            {
                return null;
            }
        }
    }

    // A jsonb column as it is; anything else (text, a constant, a parameter) cast to jsonb, since Npgsql binds a string as text.
    private SqlExpression AsJsonb(SqlExpression expression)
    {
        expression = Unwrap(expression);
        if ((expression is not (SqlConstantExpression or SqlParameterExpression))
            && string.Equals(expression.TypeMapping?.StoreType, Jsonb, StringComparison.OrdinalIgnoreCase))
        {
            return expression;
        }

        var text = sqlExpressionFactory.ApplyDefaultTypeMapping(expression);
        return sqlExpressionFactory.Convert(text, typeof(string), typeMappingSource.FindMapping(typeof(string), Jsonb));
    }

    // The object parameters arrive boxed.
    private static SqlExpression Unwrap(SqlExpression expression)
    {
        while (expression is SqlUnaryExpression { OperatorType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }
}

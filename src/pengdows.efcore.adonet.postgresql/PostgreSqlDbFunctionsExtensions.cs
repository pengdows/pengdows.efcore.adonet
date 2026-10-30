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
}

/// <summary>Translates <see cref="PostgreSqlDbFunctionsExtensions" />.</summary>
internal sealed class PostgreSqlDbFunctionsTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } = [new PostgreSqlDbFunctionsTranslator(sqlExpressionFactory, typeMappingSource)];
}

internal sealed class PostgreSqlDbFunctionsTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        // EF.Functions.Random() is random(), a double from zero up to but not including one; EF's default is RAND(), which PostgreSQL lacks.
        if ((method.DeclaringType == typeof(DbFunctionsExtensions)) && (method.Name == nameof(DbFunctionsExtensions.Random)) && (arguments.Count == 1))
        {
            return sqlExpressionFactory.Call("random()", [], typeof(double), typeMappingSource.FindMapping(typeof(double)));
        }

        if ((method.DeclaringType != typeof(PostgreSqlDbFunctionsExtensions)) || (method.Name != nameof(PostgreSqlDbFunctionsExtensions.ILike)))
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

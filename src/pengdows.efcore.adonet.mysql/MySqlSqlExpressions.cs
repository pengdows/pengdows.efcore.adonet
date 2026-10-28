using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.mysql;

internal static class MySqlSqlExpressions
{
    /// <summary>
    ///     A template function that may be null even when no argument is (JSON_EXTRACT of a missing path, CONVERT_TZ of an
    ///     unknown zone): EF must not reduce its IS NULL to its arguments'.
    /// </summary>
    public static SqlExpression CallMaybeNull(
        this ISqlExpressionFactory sqlExpressionFactory,
        string template,
        IReadOnlyList<SqlExpression> arguments,
        Type type,
        RelationalTypeMapping? mapping)
    {
        var call = (SqlFunctionExpression)sqlExpressionFactory.Call(template, arguments, type, mapping)!;
        return sqlExpressionFactory.Function(call.Name, call.Arguments!, nullable: true, call.Arguments!.Select(_ => false), type, mapping);
    }
}

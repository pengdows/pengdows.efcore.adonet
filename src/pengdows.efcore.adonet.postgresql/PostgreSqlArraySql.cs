using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     The SQL of PostgreSQL's array operators, as templates (<see cref="SqlTemplate" />): an operand that is itself an
///     operation is parenthesized, since every operator here has the same precedence as <c>||</c>.
/// </summary>
internal static class PostgreSqlArraySql
{
    public const string AnyOperator = " = ANY ";

    public static SqlExpression Any(ISqlExpressionFactory factory, SqlExpression item, SqlExpression array, RelationalTypeMapping boolMapping)
        => factory.Call(Operand(item, 0) + AnyOperator + "(" + "{1})", [item, array], typeof(bool), boolMapping)!;

    public static SqlExpression Binary(
        ISqlExpressionFactory factory,
        string @operator,
        SqlExpression left,
        SqlExpression right,
        Type type,
        RelationalTypeMapping? typeMapping)
        => factory.Call(Operand(left, 0) + " " + @operator + " " + Operand(right, 1), [left, right], type, typeMapping)!;

    /// <summary>Whether <paramref name="expression" /> is <c>item = ANY (array)</c>, and its operands.</summary>
    public static bool IsAny(SqlExpression expression, [NotNullWhen(true)] out SqlExpression? item, [NotNullWhen(true)] out SqlExpression? array)
    {
        if ((expression is SqlFunctionExpression { Arguments: [var i, var a] } function)
            && SqlTemplate.TryGetTemplate(function, out var template)
            && template.Contains(AnyOperator, StringComparison.Ordinal))
        {
            item = i;
            array = a;
            return true;
        }

        item = null;
        array = null;
        return false;
    }

    private static string Operand(SqlExpression expression, int index)
        => expression is ColumnExpression or SqlConstantExpression or SqlParameterExpression
                or SqlUnaryExpression { OperatorType: ExpressionType.Convert }
            ||((expression is SqlFunctionExpression function) && !SqlTemplate.TryGetTemplate(function, out _))
            ? "{" + index + "}"
            : "({" + index + "})";
}

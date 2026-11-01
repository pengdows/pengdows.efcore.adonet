using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     Folds a CASE whose tests compare integer constants: EF writes a constant index's paging offset as
///     <c>CASE WHEN 1 &lt; 0 THEN 0 ELSE 1 END</c>, which a paging clause that takes a value (LIMIT or OFFSET, for example) rejects.
///     The result is the branch the constants select, so the clause takes the value itself.
/// </summary>
internal static class ConstantCaseFolder
{
    public static bool TryFold(CaseExpression expression, out SqlExpression? folded)
    {
        foreach (var clause in expression.WhenClauses)
        {
            if (!TryEvaluate(clause.Test, out var holds))
            {
                folded = null;
                return false;
            }

            if (holds)
            {
                folded = clause.Result;
                return true;
            }
        }

        folded = expression.ElseResult;
        return folded is not null;
    }

    private static bool TryEvaluate(SqlExpression test, out bool holds)
    {
        holds = false;
        if (test is not SqlBinaryExpression { Left: SqlConstantExpression left, Right: SqlConstantExpression right } binary
            || !TryInteger(left.Value, out var a)
            || !TryInteger(right.Value, out var b))
        {
            return false;
        }

        switch (binary.OperatorType)
        {
            case ExpressionType.LessThan:
                holds = a < b;
                return true;
            case ExpressionType.LessThanOrEqual:
                holds = a <= b;
                return true;
            case ExpressionType.GreaterThan:
                holds = a > b;
                return true;
            case ExpressionType.GreaterThanOrEqual:
                holds = a >= b;
                return true;
            case ExpressionType.Equal:
                holds = a == b;
                return true;
            case ExpressionType.NotEqual:
                holds = a != b;
                return true;
            default:
                return false;
        }
    }

    private static bool TryInteger(object? value, out long result)
    {
        result = 0;
        switch (value)
        {
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case short s:
                result = s;
                return true;
            default:
                return false;
        }
    }
}

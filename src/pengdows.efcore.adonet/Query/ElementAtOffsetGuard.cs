using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     EF's paging offset for a parameter index is <c>CASE WHEN @p &lt; 0 THEN 0 ELSE @p END</c>, beside a predicate
///     <c>@p &gt;= 0</c> that already excludes a negative index. Where the predicate guards the parameter, the offset is the
///     parameter itself, which a paging clause that takes a value accepts.
/// </summary>
internal static class ElementAtOffsetGuard
{
    public static HashSet<string> GuardedParameters(SqlExpression? predicate)
    {
        var guarded = new HashSet<string>(StringComparer.Ordinal);
        Collect(predicate, guarded);
        return guarded;
    }

    public static bool TryUnwrap(CaseExpression offset, HashSet<string> guarded, out SqlParameterExpression? parameter)
    {
        parameter = null;
        if (offset is not { WhenClauses: [{ Test: SqlBinaryExpression { OperatorType: ExpressionType.LessThan, Left: SqlParameterExpression tested, Right: SqlConstantExpression { Value: var bound } }, Result: SqlConstantExpression { Value: var zero } }] }
            || offset.ElseResult is not SqlParameterExpression otherwise
            || tested.Name != otherwise.Name
            || !IsZero(bound)
            || !IsZero(zero)
            || !guarded.Contains(tested.Name))
        {
            return false;
        }

        parameter = otherwise;
        return true;
    }

    private static void Collect(SqlExpression? expression, HashSet<string> guarded)
    {
        switch (expression)
        {
            case SqlBinaryExpression { OperatorType: ExpressionType.AndAlso, Left: var left, Right: var right }:
                Collect(left, guarded);
                Collect(right, guarded);
                break;
            case SqlBinaryExpression { OperatorType: ExpressionType.GreaterThanOrEqual, Left: SqlParameterExpression parameter, Right: SqlConstantExpression { Value: var bound } }
                when IsZero(bound):
                guarded.Add(parameter.Name);
                break;
        }
    }

    private static bool IsZero(object? value)
        => value is int i ? i == 0 : value is long l && l == 0;
}

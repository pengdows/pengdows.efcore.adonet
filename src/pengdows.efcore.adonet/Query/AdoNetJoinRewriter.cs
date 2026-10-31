using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     Rewrites <c>string.Join(separator, [a, b, ...])</c> into concatenation before EF translates the query, since an array literal
///     has no SQL form of its own. The query compiler applies it. .NET's Join treats a null element as an empty string, so each element and the separator are coalesced to
///     <c>''</c>; EF then translates the coalesces and the concatenations as it does any other.
/// </summary>
internal sealed class AdoNetJoinRewriter : ExpressionVisitor
{
    private static readonly MethodInfo Concat = typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!;

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(string) || node.Method.Name != nameof(string.Join) || node.Arguments.Count != 2
            || node.Arguments[1] is not NewArrayExpression { NodeType: ExpressionType.NewArrayInit } array)
        {
            return base.VisitMethodCall(node);
        }

        var separator = Coalesced(Visit(node.Arguments[0]));
        var parts = array.Expressions.Select(e => Coalesced(Visit(e))).ToList();
        if (parts.Count == 0)
        {
            return Expression.Constant(string.Empty);
        }

        var joined = parts[0];
        for (var i = 1; i < parts.Count; i++)
        {
            joined = Expression.Call(Concat, Expression.Call(Concat, joined, separator), parts[i]);
        }

        return joined;
    }

    private static Expression Coalesced(Expression value)
        => Expression.Coalesce(value.Type == typeof(string) ? value : Expression.Convert(value, typeof(string)), Expression.Constant(string.Empty));
}

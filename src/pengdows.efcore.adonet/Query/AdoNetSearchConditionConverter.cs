using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     For dialects without boolean expressions (see <see cref="AdoNetSqlDialect.SupportsBooleanExpressions" />):
///     a bool value where a predicate is required becomes <c>x = TRUE</c>, and a condition where a value is required
///     becomes <c>CASE WHEN condition THEN TRUE ELSE FALSE END</c>. Runs after null semantics have been applied.
/// </summary>
public class AdoNetSearchConditionConverter(ISqlExpressionFactory sqlExpressionFactory) : ExpressionVisitor
{
    private bool _isPredicate;

    protected override Expression VisitExtension(Expression node)
    {
        switch (node)
        {
            case ShapedQueryExpression shaped:
                return shaped.UpdateQueryExpression(Value(shaped.QueryExpression));
            case SelectExpression select:
                return select.Update(
                    select.Tables.Select(t => Value(t)).ToList(),
                    Predicate(select.Predicate),
                    select.GroupBy.Select(g => Value(g)).ToList(),
                    Predicate(select.Having),
                    select.Projection.Select(p => p.Update(Value(p.Expression))).ToList(),
                    select.Orderings.Select(o => o.Update(Value(o.Expression))).ToList(),
                    Value(select.Offset),
                    Value(select.Limit));
            case InnerJoinExpression join:
                return join.Update(Value(join.Table), Predicate(join.JoinPredicate));
            case LeftJoinExpression join:
                return join.Update(Value(join.Table), Predicate(join.JoinPredicate));
            case RightJoinExpression join:
                return join.Update(Value(join.Table), Predicate(join.JoinPredicate));
            case not SqlExpression:
                return ValueChildren(node);
        }

        var isPredicate = _isPredicate;
        var (visited, isCondition) = node switch
        {
            SqlBinaryExpression { OperatorType: ExpressionType.AndAlso or ExpressionType.OrElse } binary
                => (binary.Update(Predicate(binary.Left), Predicate(binary.Right)), true),
            SqlBinaryExpression
                {
                    OperatorType: ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.LessThan
                    or ExpressionType.LessThanOrEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
                } binary
                => (binary.Update(Value(binary.Left), Value(binary.Right)), true),
            SqlUnaryExpression { OperatorType: ExpressionType.Not } unary when unary.Type == typeof(bool)
                => (unary.Update(Predicate(unary.Operand)), true),
            SqlUnaryExpression { OperatorType: ExpressionType.Equal or ExpressionType.NotEqual } unary // IS [NOT] NULL
                => (unary.Update(Value(unary.Operand)), true),
            CaseExpression caseExpression
                => (caseExpression.Update(
                    Value(caseExpression.Operand),
                    caseExpression.WhenClauses
                        .Select(c => new CaseWhenClause(
                            caseExpression.Operand is null ? Predicate(c.Test) : Value(c.Test), Value(c.Result)))
                        .ToList(),
                    Value(caseExpression.ElseResult)), false),
            ExistsExpression or InExpression or LikeExpression
                => ((SqlExpression)ValueChildren(node), true),
            _ => ((SqlExpression)ValueChildren(node), false)
        };

        if (isPredicate && !isCondition && visited.Type == typeof(bool))
        {
            return sqlExpressionFactory.Equal(visited, Constant(true));
        }

        if (!isPredicate && isCondition)
        {
            return sqlExpressionFactory.Case([new CaseWhenClause(visited, Constant(true))], Constant(false));
        }

        return visited;
    }

    private SqlExpression Constant(bool value)
        => sqlExpressionFactory.ApplyDefaultTypeMapping(new SqlConstantExpression(value, typeof(bool), typeMapping: null));

    private Expression ValueChildren(Expression node)
    {
        var parent = _isPredicate;
        _isPredicate = false;
        var result = base.VisitExtension(node);
        _isPredicate = parent;
        return result;
    }

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(node))]
    private T? Value<T>(T? node) where T : Expression
        => VisitIn(node, isPredicate: false);

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(node))]
    private T? Predicate<T>(T? node) where T : Expression
        => VisitIn(node, isPredicate: true);

    private T? VisitIn<T>(T? node, bool isPredicate) where T : Expression
    {
        if (node is null)
        {
            return null;
        }

        var parent = _isPredicate;
        _isPredicate = isPredicate;
        var result = (T)Visit(node);
        _isPredicate = parent;
        return result;
    }
}

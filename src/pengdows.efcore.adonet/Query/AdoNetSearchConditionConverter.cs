using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     For <see cref="BooleanSyntax.BitValues" />: a bool value where a search condition is required becomes <c>x = TRUE</c>
///     (a constant, <c>1 = 1</c> or <c>0 = 1</c>), and a search condition where a value is required becomes
///     <c>CASE WHEN condition THEN TRUE ELSE FALSE END</c>. With bitwise operators, comparisons and negations of bool values
///     stay values: <c>a ^ b</c>, <c>~(a ^ b)</c>, <c>~a</c>. Runs after null semantics have been applied.
/// </summary>
/// <remarks>
///     Independent implementation. EF Core SQL Server <c>SearchConditionConverter</c> (MIT, .NET Foundation; Behaviour reference (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SearchConditionConverter.cs)
/// </remarks>
internal class AdoNetSearchConditionConverter(ISqlExpressionFactory sqlExpressionFactory, bool bitwiseOperators = false) : ExpressionVisitor
{
    [return: NotNullIfNotNull(nameof(expression))]
    public override Expression? Visit(Expression? expression)
        => Visit(expression, inSearchConditionContext: false, allowNullFalseEquivalence: false);

    [return: NotNullIfNotNull(nameof(expression))]
    protected virtual Expression? Visit(Expression? expression, bool inSearchConditionContext, bool allowNullFalseEquivalence)
        => expression switch
        {
            CaseExpression e => VisitCase(e, inSearchConditionContext, allowNullFalseEquivalence),
            SelectExpression e => VisitSelect(e),
            SqlBinaryExpression e => VisitSqlBinary(e, inSearchConditionContext, allowNullFalseEquivalence),
            SqlUnaryExpression e => VisitSqlUnary(e, inSearchConditionContext),
            PredicateJoinExpressionBase e => VisitPredicateJoin(e),
            // Search conditions only: they can stand in a WHERE but not be projected.
            SqlExpression e and (ExistsExpression or InExpression or LikeExpression)
                => ApplyConversion((SqlExpression)base.VisitExtension(e), inSearchConditionContext, isExpressionSearchCondition: true),
            SqlExpression e
                => ApplyConversion((SqlExpression)base.VisitExtension(e), inSearchConditionContext, isExpressionSearchCondition: false),
            _ => base.Visit(expression)
        };

    private SqlExpression ApplyConversion(SqlExpression sqlExpression, bool inSearchConditionContext, bool isExpressionSearchCondition)
        => (inSearchConditionContext, isExpressionSearchCondition) switch
        {
            // WHERE b.BitColumn => WHERE b.BitColumn = TRUE; WHERE FALSE => WHERE 0 = 1
            (true, false) => sqlExpression is SqlConstantExpression { Value: bool value }
                ? sqlExpressionFactory.Equal(sqlExpressionFactory.Constant(value ? 1 : 0), sqlExpressionFactory.Constant(1))
                : sqlExpressionFactory.Equal(sqlExpression, Constant(true)),
            // SELECT a LIKE b => SELECT CASE WHEN a LIKE b THEN TRUE ELSE FALSE END
            (false, true) => sqlExpressionFactory.Case(
                [new CaseWhenClause(SimplifyNegatedBinary(sqlExpression), Constant(true))],
                Constant(false)),
            _ => sqlExpression
        };

    private SqlExpression Constant(bool value)
        => sqlExpressionFactory.ApplyDefaultTypeMapping(sqlExpressionFactory.Constant(value));

    // NOT (a = TRUE) => a = FALSE; NOT (a = b) => a <> b; NOT (a <> b) => a = b
    private SqlExpression SimplifyNegatedBinary(SqlExpression sqlExpression)
    {
        if (sqlExpression is SqlUnaryExpression { OperatorType: ExpressionType.Not, Operand: SqlBinaryExpression { OperatorType: ExpressionType.NotEqual } notEqual }
            && (sqlExpression.Type == typeof(bool)))
        {
            return sqlExpressionFactory.MakeBinary(ExpressionType.Equal, notEqual.Left, notEqual.Right, notEqual.TypeMapping)!;
        }

        if ((sqlExpression is not SqlUnaryExpression { OperatorType: ExpressionType.Not, Operand: SqlBinaryExpression { OperatorType: ExpressionType.Equal } equal })
            || (sqlExpression.Type != typeof(bool)))
        {
            return sqlExpression;
        }

        if (equal.Left.Type == typeof(bool) && equal.Right.Type == typeof(bool)
            && (equal.Left is SqlConstantExpression || equal.Right is SqlConstantExpression))
        {
            return equal.Left is SqlConstantExpression left
                ? sqlExpressionFactory.MakeBinary(
                    ExpressionType.Equal, sqlExpressionFactory.Constant(!(bool)left.Value!, left.TypeMapping), equal.Right, equal.TypeMapping)!
                : sqlExpressionFactory.MakeBinary(
                    ExpressionType.Equal, equal.Left,
                    sqlExpressionFactory.Constant(!(bool)((SqlConstantExpression)equal.Right).Value!, equal.Right.TypeMapping),
                    equal.TypeMapping)!;
        }

        return sqlExpressionFactory.MakeBinary(ExpressionType.NotEqual, equal.Left, equal.Right, equal.TypeMapping)!;
    }

    protected virtual Expression VisitCase(CaseExpression caseExpression, bool inSearchConditionContext, bool allowNullFalseEquivalence)
    {
        var testIsCondition = caseExpression.Operand is null;
        var operand = (SqlExpression?)Visit(caseExpression.Operand);
        var whenClauses = caseExpression.WhenClauses
            .Select(c => new CaseWhenClause(
                (SqlExpression)Visit(c.Test, testIsCondition, testIsCondition),
                (SqlExpression)Visit(c.Result, inSearchConditionContext: false, allowNullFalseEquivalence)))
            .ToList();
        var elseResult = (SqlExpression?)Visit(caseExpression.ElseResult, inSearchConditionContext: false, allowNullFalseEquivalence);

        return ApplyConversion(
            sqlExpressionFactory.Case(operand, whenClauses, elseResult, caseExpression),
            inSearchConditionContext,
            isExpressionSearchCondition: false);
    }

    protected virtual Expression VisitPredicateJoin(PredicateJoinExpressionBase join)
        => join.Update(
            (TableExpressionBase)Visit(join.Table),
            (SqlExpression)Visit(join.JoinPredicate, inSearchConditionContext: true, allowNullFalseEquivalence: true));

    protected virtual Expression VisitSelect(SelectExpression select)
        => select.Update(
            All(select.Tables),
            (SqlExpression?)Visit(select.Predicate, inSearchConditionContext: true, allowNullFalseEquivalence: true),
            All(select.GroupBy),
            (SqlExpression?)Visit(select.Having, inSearchConditionContext: true, allowNullFalseEquivalence: true),
            All(select.Projection),
            All(select.Orderings),
            (SqlExpression?)Visit(select.Offset),
            (SqlExpression?)Visit(select.Limit));

    protected virtual Expression VisitSqlBinary(SqlBinaryExpression binary, bool inSearchConditionContext, bool allowNullFalseEquivalence)
    {
        // Only logical operators need conditions on both sides.
        var operandsAreConditions = binary.OperatorType is ExpressionType.AndAlso or ExpressionType.OrElse;
        var left = (SqlExpression)Visit(binary.Left, operandsAreConditions, allowNullFalseEquivalence: false);
        var right = (SqlExpression)Visit(binary.Right, operandsAreConditions, allowNullFalseEquivalence: false);

        if (bitwiseOperators && binary.OperatorType is ExpressionType.NotEqual or ExpressionType.Equal)
        {
            if (!inSearchConditionContext && !allowNullFalseEquivalence && IsBitOrInteger(left) && IsBitOrInteger(right))
            {
                // a <> b is a ^ b as a value; a = b is ~(a ^ b).
                var result = sqlExpressionFactory.MakeBinary(ExpressionType.ExclusiveOr, left, right, null)!;
                if (result.Type != typeof(bool))
                {
                    result = sqlExpressionFactory.Convert(result, typeof(bool), binary.TypeMapping);
                }

                return binary.OperatorType is ExpressionType.Equal
                    ? sqlExpressionFactory.MakeUnary(ExpressionType.OnesComplement, result, result.Type, result.TypeMapping)!
                    : result;
            }

            if (left is SqlUnaryExpression { OperatorType: ExpressionType.OnesComplement } negatedLeft
                && right is SqlUnaryExpression { OperatorType: ExpressionType.OnesComplement } negatedRight)
            {
                left = negatedLeft.Operand;
                right = negatedRight.Operand;
            }
        }

        binary = binary.Update(left, right);
        var isCondition = binary.OperatorType is ExpressionType.AndAlso or ExpressionType.OrElse or ExpressionType.Equal
            or ExpressionType.NotEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual;

        return ApplyConversion(binary, inSearchConditionContext, isCondition);
    }

    protected virtual Expression VisitSqlUnary(SqlUnaryExpression unary, bool inSearchConditionContext)
    {
        bool operandIsCondition, isCondition;
        switch (unary.OperatorType)
        {
            case ExpressionType.Not when (unary.TypeMapping?.Converter?.ProviderClrType ?? unary.Type) == typeof(bool):
                // As a value, keep it one: ~a, rather than converting to a condition and back.
                if (bitwiseOperators && !inSearchConditionContext && unary.Operand is not (ExistsExpression or InExpression or LikeExpression))
                {
                    var negated = (SqlExpression)Visit(unary.Operand);
                    return negated is SqlUnaryExpression { OperatorType: ExpressionType.OnesComplement } doubled
                        ? doubled.Operand
                        : sqlExpressionFactory.MakeUnary(ExpressionType.OnesComplement, negated, negated.Type, negated.TypeMapping)!;
                }

                operandIsCondition = true;
                isCondition = true;
                break;
            case ExpressionType.Equal or ExpressionType.NotEqual: // IS [NOT] NULL
                operandIsCondition = false;
                isCondition = true;
                break;
            default:
                operandIsCondition = false;
                isCondition = false;
                break;
        }

        var operand = (SqlExpression)Visit(unary.Operand, operandIsCondition, allowNullFalseEquivalence: false);
        return SimplifyNegatedBinary(ApplyConversion(unary.Update(operand), inSearchConditionContext, isCondition));
    }

    private List<T> All<T>(IReadOnlyList<T> expressions) where T : Expression
        => expressions.Select(e => (T)Visit(e)).ToList();

    private static bool IsBitOrInteger(SqlExpression expression)
        => (expression.TypeMapping?.Converter?.ProviderClrType ?? expression.Type) is var type
            && (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
                || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong));
}

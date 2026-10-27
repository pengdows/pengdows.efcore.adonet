using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     Runs once parameter values are known. For dialects that reject parameters in OFFSET/FETCH, replaces them with
///     constants; reading the values through <see cref="ParametersCacheDecorator.GetAndDisableCaching" /> keeps EF
///     from caching SQL that now depends on them. Then applies the dialect's <see cref="BooleanSyntax" />.
/// </summary>
internal class AdoNetParameterBasedSqlProcessor(
    RelationalParameterBasedSqlProcessorDependencies dependencies,
    RelationalParameterBasedSqlProcessorParameters parameters,
    ISqlDialect dialect)
    : RelationalParameterBasedSqlProcessor(dependencies, parameters)
{
    public override Expression Process(Expression queryExpression, ParametersCacheDecorator parametersDecorator)
    {
        queryExpression = base.Process(queryExpression, parametersDecorator);

        queryExpression = new PagingParameterInliner(
            parametersDecorator,
            dialect.Paging.InlinesValues,
            dialect.Paging.RequiresZeroLimitInlining).Visit(queryExpression);

        // After null semantics, which add conditions of their own.
        return dialect.Booleans.Process(queryExpression, Dependencies.SqlExpressionFactory);
    }

    protected override Expression ProcessSqlNullability(Expression queryExpression, ParametersCacheDecorator decorator)
        => new AdoNetSqlNullabilityProcessor(Dependencies, Parameters, dialect).Process(queryExpression, decorator);

    private sealed class PagingParameterInliner(
        ParametersCacheDecorator parametersDecorator,
        bool inlineAll,
        bool inlineZeroLimit) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
        {
            var visited = base.VisitExtension(node);
            if (visited is not SelectExpression select || (select.Offset is not SqlParameterExpression && select.Limit is not SqlParameterExpression))
            {
                return visited;
            }

            var values = inlineAll ? parametersDecorator.GetAndDisableCaching() : null;
            if (!inlineAll && inlineZeroLimit && select.Limit is SqlParameterExpression limitParameter)
            {
                var currentValues = parametersDecorator.GetAndDisableCaching();
                if (currentValues.TryGetValue(limitParameter.Name, out var limitValue) && limitValue is int { } intValue && intValue == 0)
                {
                    values = currentValues;
                }
                else
                {
                    return visited;
                }
            }

            return select.Update(
                select.Tables,
                select.Predicate,
                select.GroupBy,
                select.Having,
                select.Projection,
                select.Orderings,
                Inline(select.Offset, values),
                Inline(select.Limit, values));
        }

        private SqlExpression? Inline(SqlExpression? expression, IReadOnlyDictionary<string, object?>? values = null)
        {
            if (expression is not SqlParameterExpression parameter)
            {
                return expression;
            }

            if (values == null || !values.TryGetValue(parameter.Name, out var value))
            {
                return expression;
            }

            return new SqlConstantExpression(value, parameter.Type, parameter.TypeMapping);
        }
    }
}

internal class AdoNetParameterBasedSqlProcessorFactory(
    RelationalParameterBasedSqlProcessorDependencies dependencies,
    ISqlDialect dialect)
    : IRelationalParameterBasedSqlProcessorFactory
{
    public virtual RelationalParameterBasedSqlProcessor Create(RelationalParameterBasedSqlProcessorParameters parameters)
        => new AdoNetParameterBasedSqlProcessor(dependencies, parameters, dialect);
}

/// <summary>EF's null semantics, with the dialect's preference between nullable IN and EXISTS.</summary>
internal class AdoNetSqlNullabilityProcessor(
    RelationalParameterBasedSqlProcessorDependencies dependencies,
    RelationalParameterBasedSqlProcessorParameters parameters,
    ISqlDialect dialect)
    : SqlNullabilityProcessor(dependencies, parameters)
{
    protected override bool PreferExistsToInWithCoalesce
        => dialect.PreferExistsToIn;

    protected override SqlExpression VisitCustomSqlExpression(SqlExpression sqlExpression, bool allowOptimizedExpansion, out bool nullable)
    {
        if (sqlExpression is not RowValueSqlExpression rowValue)
        {
            return base.VisitCustomSqlExpression(sqlExpression, allowOptimizedExpansion, out nullable);
        }

        // The null-versus-false distinction matters inside the row, so no optimized expansion; the row itself is never null.
        nullable = false;
        return rowValue.Update(rowValue.Values.Select(v => Visit(v, allowOptimizedExpansion: false, out _)).ToList());
    }

    protected override SqlExpression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression, bool allowOptimizedExpansion, out bool nullable)
        => sqlBinaryExpression is
            {
                OperatorType: ExpressionType.Equal or ExpressionType.NotEqual,
                Left: RowValueSqlExpression left,
                Right: RowValueSqlExpression right
            }
            ? VisitRowValueComparison(sqlBinaryExpression, left, right, allowOptimizedExpansion, out nullable)
            : base.VisitSqlBinary(sqlBinaryExpression, allowOptimizedExpansion, out nullable);

    // SQL row equality propagates NULL, while .NET tuple equality treats two null elements as equal. Keep the
    // non-nullable portion as a row and expand only the element comparisons whose null state can change the result.
    // This is an independent implementation of the required behavior.
    // Behaviour reference (not copied):
    // Npgsql (Behaviour reference (not copied): https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Internal/NpgsqlSqlNullabilityProcessor.cs)
    private SqlExpression VisitRowValueComparison(
        SqlBinaryExpression comparison,
        RowValueSqlExpression left,
        RowValueSqlExpression right,
        bool allowOptimizedExpansion,
        out bool nullable)
    {
        var equals = comparison.OperatorType == ExpressionType.Equal;
        var rowLeft = new List<SqlExpression>(left.Values.Count);
        var rowRight = new List<SqlExpression>(right.Values.Count);
        var elementComparisons = new List<SqlExpression>();

        for (var index = 0; index < left.Values.Count; index++)
        {
            var leftValue = Visit(left.Values[index], out var leftMayBeNull);
            var rightValue = Visit(right.Values[index], out var rightMayBeNull);
            var canStayInRow = (!leftMayBeNull && !rightMayBeNull)
                || (allowOptimizedExpansion && equals && (!leftMayBeNull || !rightMayBeNull));

            if (canStayInRow)
            {
                rowLeft.Add(leftValue);
                rowRight.Add(rightValue);
                continue;
            }

            var elementComparison = Dependencies.SqlExpressionFactory.MakeBinary(
                comparison.OperatorType,
                leftValue,
                rightValue,
                typeMapping: null)!;
            elementComparisons.Add(Visit(elementComparison, allowOptimizedExpansion, out _));
        }

        nullable = false;
        if (elementComparisons.Count == 0)
        {
            return comparison.Update(left.Update(rowLeft), right.Update(rowRight));
        }

        if (rowLeft.Count > 0)
        {
            var rowComparison = rowLeft.Count == 1
                ? Dependencies.SqlExpressionFactory.MakeBinary(comparison.OperatorType, rowLeft[0], rowRight[0], null)!
                : comparison.Update(left.Update(rowLeft), right.Update(rowRight));
            elementComparisons.Insert(0, rowComparison);
        }

        var combined = elementComparisons[0];
        for (var index = 1; index < elementComparisons.Count; index++)
        {
            combined = equals
                ? Dependencies.SqlExpressionFactory.AndAlso(combined, elementComparisons[index])
                : Dependencies.SqlExpressionFactory.OrElse(combined, elementComparisons[index]);
        }

        return combined;
    }
}

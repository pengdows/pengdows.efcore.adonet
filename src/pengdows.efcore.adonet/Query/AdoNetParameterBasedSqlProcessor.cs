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

        if (dialect.Paging.InlinesValues)
        {
            queryExpression = new PagingParameterInliner(parametersDecorator).Visit(queryExpression);
        }

        // After null semantics, which add conditions of their own.
        return dialect.Booleans.Process(queryExpression, Dependencies.SqlExpressionFactory);
    }

    protected override Expression ProcessSqlNullability(Expression queryExpression, ParametersCacheDecorator decorator)
        => new AdoNetSqlNullabilityProcessor(Dependencies, Parameters, dialect).Process(queryExpression, decorator);

    private sealed class PagingParameterInliner(ParametersCacheDecorator parametersDecorator) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
        {
            var visited = base.VisitExtension(node);
            if (visited is not SelectExpression select
                || (select.Offset is not SqlParameterExpression && select.Limit is not SqlParameterExpression))
            {
                return visited;
            }

            return select.Update(
                select.Tables,
                select.Predicate,
                select.GroupBy,
                select.Having,
                select.Projection,
                select.Orderings,
                Inline(select.Offset),
                Inline(select.Limit));
        }

        private SqlExpression? Inline(SqlExpression? expression)
            => expression is SqlParameterExpression parameter
                ? new SqlConstantExpression(
                    parametersDecorator.GetAndDisableCaching()[parameter.Name],
                    parameter.Type,
                    parameter.TypeMapping)
                : expression;
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

    // SQL's row equality is NULL where any pair holds a NULL; .NET's tuple equality compares nulls as values. Pairs where
    // that can differ are taken out of the row and compared on their own, with EF's null semantics, joined by AND (for =)
    // or OR (for <>); the rest stay a row value.
    // Adapted from npgsql/efcore.pg's NpgsqlSqlNullabilityProcessor (PostgreSQL License).
    private SqlExpression VisitRowValueComparison(
        SqlBinaryExpression comparison,
        RowValueSqlExpression left,
        RowValueSqlExpression right,
        bool allowOptimizedExpansion,
        out bool nullable)
    {
        var equal = comparison.OperatorType == ExpressionType.Equal;
        var keptLeft = new List<SqlExpression>();
        var keptRight = new List<SqlExpression>();
        var expanded = new List<SqlExpression>();
        for (var i = 0; i < left.Values.Count; i++)
        {
            var l = Visit(left.Values[i], out var leftNullable);
            var r = Visit(right.Values[i], out var rightNullable);
            if ((!leftNullable && !rightNullable) || (allowOptimizedExpansion && equal && (!leftNullable || !rightNullable)))
            {
                keptLeft.Add(l);
                keptRight.Add(r);
                continue;
            }

            expanded.Add(Visit(Dependencies.SqlExpressionFactory.MakeBinary(comparison.OperatorType, l, r, null)!, allowOptimizedExpansion, out _));
        }

        nullable = false;
        if (expanded.Count == 0)
        {
            return comparison.Update(left.Update(keptLeft), right.Update(keptRight));
        }

        // The pairs still compared as a row come first; a single one needs no row.
        if (keptLeft.Count > 0)
        {
            expanded.Insert(
                0,
                keptLeft.Count == 1
                    ? Dependencies.SqlExpressionFactory.MakeBinary(comparison.OperatorType, keptLeft[0], keptRight[0], null)!
                    : comparison.Update(left.Update(keptLeft), right.Update(keptRight)));
        }

        return expanded.Aggregate((a, b) => equal ? Dependencies.SqlExpressionFactory.AndAlso(a, b) : Dependencies.SqlExpressionFactory.OrElse(a, b));
    }
}

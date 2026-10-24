using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     Runs once parameter values are known. For dialects that reject parameters in OFFSET/FETCH, replaces them with
///     constants; reading the values through <see cref="ParametersCacheDecorator.GetAndDisableCaching" /> keeps EF
///     from caching SQL that now depends on them. For dialects without boolean expressions, converts between
///     bool values and conditions (<see cref="AdoNetSearchConditionConverter" />).
/// </summary>
public class AdoNetParameterBasedSqlProcessor(
    RelationalParameterBasedSqlProcessorDependencies dependencies,
    RelationalParameterBasedSqlProcessorParameters parameters,
    AdoNetSqlDialect dialect)
    : RelationalParameterBasedSqlProcessor(dependencies, parameters)
{
    public override Expression Process(Expression queryExpression, ParametersCacheDecorator parametersDecorator)
    {
        queryExpression = base.Process(queryExpression, parametersDecorator);

        if (!dialect.SupportsParametersInPaging)
        {
            queryExpression = new PagingParameterInliner(parametersDecorator).Visit(queryExpression);
        }

        // After null semantics, which add conditions of their own.
        return dialect.SupportsBooleanExpressions
            ? queryExpression
            : new AdoNetSearchConditionConverter(Dependencies.SqlExpressionFactory).Visit(queryExpression);
    }

    private sealed class PagingParameterInliner(ParametersCacheDecorator parametersDecorator) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
        {
            if (node is ShapedQueryExpression shaped)
            {
                return shaped.UpdateQueryExpression(Visit(shaped.QueryExpression));
            }

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

public class AdoNetParameterBasedSqlProcessorFactory(
    RelationalParameterBasedSqlProcessorDependencies dependencies,
    AdoNetSqlDialect dialect)
    : IRelationalParameterBasedSqlProcessorFactory
{
    public virtual RelationalParameterBasedSqlProcessor Create(RelationalParameterBasedSqlProcessorParameters parameters)
        => new AdoNetParameterBasedSqlProcessor(dependencies, parameters, dialect);
}

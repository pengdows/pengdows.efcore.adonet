using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

public partial class AdoNetQuerySqlGenerator
{
    private HashSet<string> _guardedOffsetParameters = new(StringComparer.Ordinal);

    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        _selectDepth++;
        var outerGuarded = _guardedOffsetParameters;
        _guardedOffsetParameters = ElementAtOffsetGuard.GuardedParameters(selectExpression.Predicate);
        try
        {
            if ((_selectDepth > 1)
                && dialect.RequiresDerivedTableColumnAliases
                && selectExpression.Projection.Any(projection => projection.Alias.Length == 0))
            {
                selectExpression = selectExpression.Update(
                    tables: selectExpression.Tables,
                    predicate: selectExpression.Predicate,
                    groupBy: selectExpression.GroupBy,
                    having: selectExpression.Having,
                    projections: selectExpression.Projection.Select(projection =>
                        projection.Alias.Length == 0
                            ? new ProjectionExpression(projection.Expression, $"c{_derivedProjectionAlias++}")
                            : projection).ToList(),
                    orderings: selectExpression.Orderings,
                    offset: selectExpression.Offset,
                    limit: selectExpression.Limit);
            }

            return base.VisitSelect(selectExpression);
        }
        finally
        {
            _guardedOffsetParameters = outerGuarded;
            _selectDepth--;
        }
    }

    // A CASE that folds to one of its branches (constant tests), or to the parameter a paging offset is guarded to: each is
    // written as that value, which paging takes; null for every other expression.
    private Expression? TryFoldedCase(Expression extensionExpression)
    {
        if (extensionExpression is not CaseExpression caseExpression)
        {
            return null;
        }

        if (ConstantCaseFolder.TryFold(caseExpression, out var folded) && folded is not null)
        {
            return folded;
        }

        return ElementAtOffsetGuard.TryUnwrap(caseExpression, _guardedOffsetParameters, out var parameter) ? parameter : null;
    }
}

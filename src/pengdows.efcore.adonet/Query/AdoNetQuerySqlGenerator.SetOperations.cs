using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Linq.Expressions;

namespace pengdows.efcore.adonet.Query;

// Set operations. A union's operands are written as the dialect writes them; a null the operand projects for a column it does
// not have is cast to that column's type, since some databases resolve two untyped NULLs in a UNION to text.
public partial class AdoNetQuerySqlGenerator
{
    private SelectExpression? _unionOperand;
    private bool _typingNullProjection;

    protected override void GenerateProjection(SelectExpression selectExpression)
    {
        var previous = _typingNullProjection;
        _typingNullProjection = ReferenceEquals(selectExpression, _unionOperand);
        try
        {
            base.GenerateProjection(selectExpression);
        }
        finally
        {
            _typingNullProjection = previous;
        }

        if ((_selectDepth > 1)
            && dialect.RequiresDerivedTableColumnAliases
            && (selectExpression.Projection.Count == 0))
        {
            Sql.Append(AliasSeparator)
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier($"c{_derivedProjectionAlias++}"));
        }
    }

    protected override Expression VisitSqlConstant(SqlConstantExpression sqlConstantExpression)
    {
        if (_typingNullProjection
            && (sqlConstantExpression.Value is null)
            && (sqlConstantExpression.TypeMapping?.StoreType is { } storeType))
        {
            Sql.Append(typeMappings.Cast(storeType).Replace("{0}", "NULL", StringComparison.Ordinal));
            return sqlConstantExpression;
        }

        return base.VisitSqlConstant(sqlConstantExpression);
    }

    // The operand whose projection is being written, and whether its nulls are typed. A subquery inside a projection is another
    // select, so it does not inherit the setting.
    protected override void GenerateSetOperationOperand(SetOperationBase setOperation, SelectExpression operand)
    {
        var previous = _unionOperand;
        _unionOperand = operand;
        try
        {
            if (operand.Tables is not [ValuesExpression { RowValues.Count: > 1 }])
            {
                base.GenerateSetOperationOperand(setOperation, operand);
                return;
            }

            Sql.Append("(");
            base.GenerateSetOperationOperand(setOperation, operand);
            Sql.Append(")");
        }
        finally
        {
            _unionOperand = previous;
        }
    }
}

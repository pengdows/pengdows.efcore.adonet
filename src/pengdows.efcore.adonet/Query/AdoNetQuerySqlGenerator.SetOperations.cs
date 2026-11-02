using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Linq.Expressions;

namespace pengdows.efcore.adonet.Query;

// Set operations. A union's operands are written as the dialect writes them; a null the operand projects for a column it does
// not have is cast to that column's type, since some databases resolve two untyped NULLs in a UNION to text.
public partial class AdoNetQuerySqlGenerator
{
    private SelectExpression? _unionOperand;
    private bool _typingNullProjection;
    // A select's projection expressions, by reference, and whether one of them is being written: a geometry projection is read
    // through the dialect's ReadAs, and an expression inside a projection (a function's argument) is not wrapped.
    private HashSet<Expression>? _projectedExpressions;
    private bool _projectionItemActive;

    protected override void GenerateProjection(SelectExpression selectExpression)
    {
        var previous = _typingNullProjection;
        var previousProjected = _projectedExpressions;
        var previousActive = _projectionItemActive;
        _typingNullProjection = ReferenceEquals(selectExpression, _unionOperand);
        _projectedExpressions = new HashSet<Expression>(
            selectExpression.Projection.Select(projection => (Expression)projection.Expression), ReferenceEqualityComparer.Instance);
        _projectionItemActive = false;
        try
        {
            base.GenerateProjection(selectExpression);
        }
        finally
        {
            _typingNullProjection = previous;
            _projectedExpressions = previousProjected;
            _projectionItemActive = previousActive;
        }

        if ((_selectDepth > 1)
            && dialect.RequiresDerivedTableColumnAliases
            && (selectExpression.Projection.Count == 0))
        {
            Sql.Append(AliasSeparator)
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier($"c{_derivedProjectionAlias++}"));
        }
    }

    // A geometry a select projects is read through the dialect's ReadAs, since the driver can't read the database's geometry
    // type. Only a projection item itself is wrapped: an expression visited while one is being written is part of it.
    public override Expression? Visit(Expression? node)
    {
        if ((_projectedExpressions is null) || _projectionItemActive || (node is null) || !_projectedExpressions.Contains(node))
        {
            return base.Visit(node);
        }

        _projectionItemActive = true;
        try
        {
            if (node is SqlExpression { TypeMapping.StoreType: { } storeType }
                && dialect.Spatial is { ReadAs: { } readAs } spatial
                && string.Equals(storeType, spatial.StoreType("Geometry"), StringComparison.OrdinalIgnoreCase))
            {
                var parts = readAs.Split("{0}");
                Sql.Append(parts[0]);
                base.Visit(node);
                Sql.Append(parts[1]);
                return node;
            }

            return base.Visit(node);
        }
        finally
        {
            _projectionItemActive = false;
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

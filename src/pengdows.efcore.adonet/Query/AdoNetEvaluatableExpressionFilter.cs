using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     Leaves tuples built in a query unevaluated where the dialect has row values: a row value is written from its parts
///     and can't be one parameter, so <c>new Tuple&lt;string&gt;("London")</c> must stay a construction.
/// </summary>
/// <remarks>
///     Behaviour reference (not copied):
///     Npgsql (Behaviour reference (not copied): https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Internal/NpgsqlEvaluatableExpressionFilter.cs)
/// </remarks>
internal sealed class AdoNetEvaluatableExpressionFilter(
    EvaluatableExpressionFilterDependencies dependencies,
    RelationalEvaluatableExpressionFilterDependencies relationalDependencies,
    ISqlDialect dialect)
    : RelationalEvaluatableExpressionFilter(dependencies, relationalDependencies)
{
    public override bool IsEvaluatableExpression(Expression expression, IModel model)
    {
        if (dialect.RowValues && IsTupleConstruction(expression))
        {
            return false;
        }

        return base.IsEvaluatableExpression(expression, model);
    }

    private static bool IsTupleConstruction(Expression expression)
        => expression switch
        {
            NewExpression created => created.Type.IsAssignableTo(typeof(ITuple)),
            MethodCallExpression { Method: { IsStatic: true, Name: "Create" } method }
                => (method.DeclaringType == typeof(Tuple)) || (method.DeclaringType == typeof(ValueTuple)),
            _ => false
        };
}

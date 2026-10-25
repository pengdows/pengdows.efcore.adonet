using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational postprocessing, with a type-mapping pass that knows the core's own SQL expressions.</summary>
internal sealed class AdoNetQueryTranslationPostprocessor(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    RelationalQueryCompilationContext queryCompilationContext,
    IAdoNetDialect dialect)
    : RelationalQueryTranslationPostprocessor(dependencies, relationalDependencies, queryCompilationContext)
{
    public override Expression Process(Expression query)
    {
        var processed = base.Process(query);
        if ((dialect.Sql.LateralJoins == null) && LateralJoinFinder.Finds(processed))
        {
            throw new InvalidOperationException(
                $"This query joins a subquery correlated with the rows before it (a lateral join, EF's APPLY), which {dialect.Name} doesn't have.");
        }

        return processed;
    }

    protected override Expression ProcessTypeMappings(Expression expression)
        => new AdoNetTypeMappingPostprocessor(Dependencies, RelationalDependencies, RelationalQueryCompilationContext).Process(expression);
}

/// <summary>A row value has no type mapping of its own: it is written from its values, each of which has one.</summary>
internal sealed class AdoNetTypeMappingPostprocessor(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    RelationalQueryCompilationContext queryCompilationContext)
    : RelationalTypeMappingPostprocessor(dependencies, relationalDependencies, queryCompilationContext)
{
    protected override Expression VisitExtension(Expression expression)
        => expression is RowValueSqlExpression rowValue
            ? rowValue.Update(rowValue.Values.Select(v => (Microsoft.EntityFrameworkCore.Query.SqlExpressions.SqlExpression)Visit(v)).ToList())
            : base.VisitExtension(expression);
}

/// <summary>Whether a translated query holds a CROSS or OUTER APPLY of a subquery: of a plain table, it is an ordinary join.</summary>
internal sealed class LateralJoinFinder : ExpressionVisitor
{
    private bool _found;

    public static bool Finds(Expression expression)
    {
        var finder = new LateralJoinFinder();
        finder.Visit(expression);
        return finder._found;
    }

    public override Expression? Visit(Expression? node)
    {
        if (_found)
        {
            return node;
        }

        if (node is CrossApplyExpression { Table: not TableExpression } or OuterApplyExpression { Table: not TableExpression })
        {
            _found = true;
            return node;
        }

        // A shaped query isn't visited as a whole: its query and its shaper (which holds collection subqueries) are.
        if (node is ShapedQueryExpression shaped)
        {
            Visit(shaped.QueryExpression);
            Visit(shaped.ShaperExpression);
            return node;
        }

        return base.Visit(node);
    }
}

internal sealed class AdoNetQueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    IAdoNetDialect dialect) : IQueryTranslationPostprocessorFactory
{
    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new AdoNetQueryTranslationPostprocessor(
            dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext, dialect);
}

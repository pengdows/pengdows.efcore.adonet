using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational postprocessing, with a type-mapping pass that knows the core's own SQL expressions.</summary>
internal sealed class AdoNetQueryTranslationPostprocessor(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    RelationalQueryCompilationContext queryCompilationContext)
    : RelationalQueryTranslationPostprocessor(dependencies, relationalDependencies, queryCompilationContext)
{
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

internal sealed class AdoNetQueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies) : IQueryTranslationPostprocessorFactory
{
    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new AdoNetQueryTranslationPostprocessor(dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext);
}

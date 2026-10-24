using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational LINQ translation; the dialect's <see cref="DmlSyntax" /> decides which deletes it writes directly.</summary>
public class AdoNetQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
{
    private readonly ISqlDialect _dialect;

    public AdoNetQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext,
        ISqlDialect dialect)
        : base(dependencies, relationalDependencies, queryCompilationContext)
        => _dialect = dialect;

    protected AdoNetQueryableMethodTranslatingExpressionVisitor(AdoNetQueryableMethodTranslatingExpressionVisitor parentVisitor)
        : base(parentVisitor)
        => _dialect = parentVisitor._dialect;

    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new AdoNetQueryableMethodTranslatingExpressionVisitor(this);

    protected override bool IsValidSelectExpressionForExecuteDelete(SelectExpression selectExpression)
        => _dialect.Dml.CanDeleteDirectly(selectExpression) ?? base.IsValidSelectExpressionForExecuteDelete(selectExpression);
}

public class AdoNetQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
    ISqlDialect dialect)
    : IQueryableMethodTranslatingExpressionVisitorFactory
{
    public virtual QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => new AdoNetQueryableMethodTranslatingExpressionVisitor(
            dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext, dialect);
}

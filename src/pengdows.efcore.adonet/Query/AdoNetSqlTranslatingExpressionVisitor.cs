using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     <c>StartsWith</c>, <c>EndsWith</c> and <c>Contains</c> with a parameter become <c>x LIKE @p_startswith ESCAPE '\'</c>,
///     the pattern a second parameter computed from the first with its wildcards escaped, so an index can serve the
///     prefix match. Everything else goes to the translators.
/// </summary>
/// <remarks>Follows EF Core's SQL Server provider (MIT, .NET Foundation).</remarks>
internal class AdoNetSqlTranslatingExpressionVisitor : RelationalSqlTranslatingExpressionVisitor
{
    private readonly QueryCompilationContext _queryCompilationContext;
    private readonly FunctionSyntax _functions;

    public AdoNetSqlTranslatingExpressionVisitor(
        RelationalSqlTranslatingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor,
        FunctionSyntax functions)
        : base(dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor)
    {
        _queryCompilationContext = queryCompilationContext;
        _functions = functions;
    }

    private static readonly MethodInfo PatternMethod
        = typeof(AdoNetSqlTranslatingExpressionVisitor).GetMethod(nameof(Pattern), BindingFlags.NonPublic | BindingFlags.Static)!;

    private enum Match
    {
        StartsWith,
        EndsWith,
        Contains
    }

    protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
    {
        if ((methodCallExpression is { Object: { } instance, Arguments: [var argument] })
            && (methodCallExpression.Method.DeclaringType == typeof(string))
            && (argument.Type == typeof(string))
            && (_functions.LikeEscapeCharacter is { } escape)
            && Enum.TryParse<Match>(methodCallExpression.Method.Name, out var match)
            && (Visit(argument) is SqlParameterExpression parameter)
            && (Visit(instance) is SqlExpression translatedInstance))
        {
            var mapping = ExpressionExtensions.InferTypeMapping(translatedInstance, parameter);
            var pattern = _queryCompilationContext.RegisterRuntimeParameter(
                $"{parameter.Name}_{match.ToString().ToLowerInvariant()}",
                Expression.Lambda(
                    Expression.Call(
                        PatternMethod,
                        QueryCompilationContext.QueryContextParameter,
                        Expression.Constant(parameter.Name),
                        Expression.Constant(match),
                        Expression.Constant(_functions)),
                    QueryCompilationContext.QueryContextParameter));

            return Dependencies.SqlExpressionFactory.Like(
                Dependencies.SqlExpressionFactory.ApplyTypeMapping(translatedInstance, mapping),
                new SqlParameterExpression(pattern.Name!, typeof(string), mapping),
                Dependencies.SqlExpressionFactory.Constant(escape.ToString(), mapping));
        }

        return base.VisitMethodCall(methodCallExpression);
    }

    private static string? Pattern(QueryContext queryContext, string parameterName, Match match, FunctionSyntax functions)
        => queryContext.Parameters[parameterName] is string value && functions.EscapeLikePattern(value) is { } escaped
            ? match switch
            {
                Match.StartsWith => escaped + "%",
                Match.EndsWith => "%" + escaped,
                _ => "%" + escaped + "%"
            }
            : null;
}

internal class AdoNetSqlTranslatingExpressionVisitorFactory(RelationalSqlTranslatingExpressionVisitorDependencies dependencies, ISqlDialect dialect)
    : IRelationalSqlTranslatingExpressionVisitorFactory
{
    public virtual RelationalSqlTranslatingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor)
        => new AdoNetSqlTranslatingExpressionVisitor(dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor, dialect.Functions);
}

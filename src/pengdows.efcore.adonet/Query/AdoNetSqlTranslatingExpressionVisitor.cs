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
    private readonly bool _rowValues;

    public AdoNetSqlTranslatingExpressionVisitor(
        RelationalSqlTranslatingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor,
        FunctionSyntax functions,
        bool rowValues)
        : base(dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor)
    {
        _queryCompilationContext = queryCompilationContext;
        _functions = functions;
        _rowValues = rowValues;
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
        if (_rowValues && TryTranslateTupleEquals(methodCallExpression) is { } rowComparison)
        {
            return rowComparison;
        }

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

    // tuple == other and tuple != other compare the values, as the query's expected results do. A bitwise operator the
    // database doesn't have is left untranslated.
    protected override Expression VisitBinary(BinaryExpression binaryExpression)
    {
        if (IsBitwise(binaryExpression.NodeType, binaryExpression.Type) && (BitwiseTemplate(binaryExpression.NodeType) == null))
        {
            return QueryCompilationContext.NotTranslatedExpression;
        }

        if (_rowValues
            && (binaryExpression.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            && (TryTranslateTupleComparison(binaryExpression.Left, binaryExpression.Right) is SqlBinaryExpression rowComparison))
        {
            return binaryExpression.NodeType == ExpressionType.Equal
                ? rowComparison
                : Dependencies.SqlExpressionFactory.Not(rowComparison);
        }

        return base.VisitBinary(binaryExpression);
    }

    protected override Expression VisitUnary(UnaryExpression unaryExpression)
        => IsBitwise(unaryExpression.NodeType, unaryExpression.Type) && (_functions.BitwiseNot == null)
            ? QueryCompilationContext.NotTranslatedExpression
            : base.VisitUnary(unaryExpression);

    private static bool IsBitwise(ExpressionType nodeType, Type type)
        => (nodeType is ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr or ExpressionType.Not or ExpressionType.OnesComplement)
            && ((Nullable.GetUnderlyingType(type) ?? type) != typeof(bool));

    private string? BitwiseTemplate(ExpressionType nodeType)
        => nodeType switch
        {
            ExpressionType.And => _functions.BitwiseAnd,
            ExpressionType.Or => _functions.BitwiseOr,
            _ => _functions.ExclusiveOr
        };

    // tuple.Equals(other), both built in the query: (a, b) = (x, y), each pair typed as its own comparison would be.
    private SqlExpression? TryTranslateTupleEquals(MethodCallExpression call)
        => call is { Method.Name: nameof(Equals), Object: { } instance, Arguments: [var argument] }
            ? TryTranslateTupleComparison(instance, argument)
            : null;

    private SqlExpression? TryTranslateTupleComparison(Expression instance, Expression argument)
    {
        if (!instance.Type.IsAssignableTo(typeof(System.Runtime.CompilerServices.ITuple))
            || (RowValueSqlExpression.Elements(instance) is not { } left)
            || (RowValueSqlExpression.Elements(argument) is not { } right)
            || (left.Count != right.Count))
        {
            return null;
        }

        var leftValues = new SqlExpression[left.Count];
        var rightValues = new SqlExpression[right.Count];
        for (var i = 0; i < left.Count; i++)
        {
            if ((Visit(left[i]) is not SqlExpression l)
                || (Visit(right[i]) is not SqlExpression r)
                || (Dependencies.SqlExpressionFactory.Equal(l, r) is not SqlBinaryExpression pair))
            {
                return null;
            }

            (leftValues[i], rightValues[i]) = (pair.Left, pair.Right);
        }

        return new SqlBinaryExpression(
            ExpressionType.Equal,
            new RowValueSqlExpression(leftValues, instance.Type),
            new RowValueSqlExpression(rightValues, argument.Type),
            typeof(bool),
            Dependencies.TypeMappingSource.FindMapping(typeof(bool)));
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
        => new AdoNetSqlTranslatingExpressionVisitor(
            dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor, dialect.Functions, dialect.RowValues);
}

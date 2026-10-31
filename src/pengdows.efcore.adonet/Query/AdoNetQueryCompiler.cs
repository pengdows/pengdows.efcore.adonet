#pragma warning disable EF1001 // QueryCompiler is EF Core's internal extension point; this provider replaces it deliberately.
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     Rejects ElementAt with a negative position before EF turns the position into a query parameter, so every provider
///     throws ArgumentOutOfRangeException as LINQ does, with the same behaviour on the sync and async paths. A position
///     known only at execution is read from the captured value. Behaviour reference (not copied): EF Core's QueryCompiler
///     (MIT, .NET Foundation): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore/Query/Internal/QueryCompiler.cs
/// </summary>
internal sealed class AdoNetQueryCompiler(
    IQueryContextFactory queryContextFactory,
    ICompiledQueryCache compiledQueryCache,
    ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
    IDatabase database,
    IDiagnosticsLogger<DbLoggerCategory.Query> logger,
    ICurrentDbContext currentContext,
    IEvaluatableExpressionFilter evaluatableExpressionFilter,
    IModel model)
    : QueryCompiler(
        queryContextFactory,
        compiledQueryCache,
        compiledQueryCacheKeyGenerator,
        database,
        logger,
        currentContext,
        evaluatableExpressionFilter,
        model)
{
    public override TResult Execute<TResult>(Expression query)
    {
        NegativePositionFinder.Check(query);
        return base.Execute<TResult>(query);
    }

    public override TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken = default)
    {
        NegativePositionFinder.Check(query);
        return base.ExecuteAsync<TResult>(query, cancellationToken);
    }

    private sealed class NegativePositionFinder : ExpressionVisitor
    {
        public static void Check(Expression query) => new NegativePositionFinder().Visit(query);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable)
                && node.Method.Name == nameof(Queryable.ElementAt)
                && node.Arguments.Count == 2
                && PositionOf(node.Arguments[1]) is { } position
                && position < 0)
            {
                throw new ArgumentOutOfRangeException("index", position, "The position must not be negative.");
            }

            return base.VisitMethodCall(node);
        }

        // The literal position, or a position computed from captured values only. A position that depends on the
        // query's own parameters is not known until the query runs, so it is left alone here.
        private static int? PositionOf(Expression index)
        {
            if (index is ConstantExpression { Value: int literal })
            {
                return literal;
            }

            if (new ParameterFinder().Find(index))
            {
                return null;
            }

            return Expression.Lambda<Func<object?>>(Expression.Convert(index, typeof(object))).Compile()() is int computed
                ? computed
                : null;
        }
    }

    private sealed class ParameterFinder : ExpressionVisitor
    {
        private bool _found;

        public bool Find(Expression expression)
        {
            Visit(expression);
            return _found;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _found = true;
            return node;
        }
    }
}
#pragma warning restore EF1001

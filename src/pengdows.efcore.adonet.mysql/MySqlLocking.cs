using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     Row-level locking reads: <c>SELECT ... FOR UPDATE</c>/<c>FOR SHARE</c>, inside a transaction. Written as a query tag,
///     read back by <see cref="MySqlQuerySqlGenerator" />, as Pomelo's locking extensions are. Behaviour reference (not
///     copied): https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/1217f926be3771c016eb26b8192119569902d215/src/EFCore.MySql/Extensions/MySqlLockingQueryableExtensions.cs
/// </summary>
public static class MySqlLockingQueryableExtensions
{
    internal const string ForUpdateTag = "pengdows:ForUpdate";
    internal const string ForShareTag = "pengdows:ForShare";
    internal const string ForUpdateNoWaitTag = "pengdows:ForUpdateNoWait";
    internal const string ForUpdateSkipLockedTag = "pengdows:ForUpdateSkipLocked";

    /// <summary>Locks every row the query reads for update: <c>FOR UPDATE</c>, blocking until a conflicting lock is released.</summary>
    public static IQueryable<T> ForUpdate<T>(this IQueryable<T> source)
        => source.TagWith(ForUpdateTag);

    /// <summary>Locks every row the query reads against an update: <c>FOR SHARE</c>, blocking until a conflicting lock is released.</summary>
    public static IQueryable<T> ForShare<T>(this IQueryable<T> source)
        => source.TagWith(ForShareTag);

    /// <summary><see cref="ForUpdate{T}" />, failing immediately (<c>ER_LOCK_NOWAIT</c>) instead of waiting for a conflicting lock.</summary>
    public static IQueryable<T> ForUpdateNoWait<T>(this IQueryable<T> source)
        => source.TagWith(ForUpdateNoWaitTag);

    /// <summary><see cref="ForUpdate{T}" />, skipping rows a conflicting lock already holds instead of waiting for them.</summary>
    public static IQueryable<T> ForUpdateSkipLocked<T>(this IQueryable<T> source)
        => source.TagWith(ForUpdateSkipLockedTag);
}

/// <summary>Appends the lock clause a <see cref="MySqlLockingQueryableExtensions" /> tag asks for, after the query it tags.</summary>
internal sealed class MySqlQuerySqlGenerator(
    QuerySqlGeneratorDependencies dependencies, ISqlDialect dialect, ITypeMappingDialect typeMappings, bool reverseNullOrdering)
    : pengdows.efcore.adonet.Query.AdoNetQuerySqlGenerator(dependencies, dialect, typeMappings, reverseNullOrdering)
{
    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        // Read only: never remove a tag, so the same immutable expression tree can be visited (and cached) again.
        var tags = selectExpression.Tags;
        var lockClause = tags.Contains(MySqlLockingQueryableExtensions.ForUpdateTag) ? " FOR UPDATE"
            : tags.Contains(MySqlLockingQueryableExtensions.ForUpdateNoWaitTag) ? " FOR UPDATE NOWAIT"
            : tags.Contains(MySqlLockingQueryableExtensions.ForUpdateSkipLockedTag) ? " FOR UPDATE SKIP LOCKED"
            : tags.Contains(MySqlLockingQueryableExtensions.ForShareTag) ? " FOR SHARE"
            : null;

        var result = base.VisitSelect(selectExpression);
        if (lockClause != null)
        {
            Sql.Append(lockClause);
        }

        return result;
    }
}

internal sealed class MySqlQuerySqlGeneratorFactory(
    QuerySqlGeneratorDependencies dependencies, ISqlDialect dialect, ITypeMappingDialect typeMappings, pengdows.efcore.adonet.Query.IAdoNetSingletonOptions options)
    : IQuerySqlGeneratorFactory
{
    public QuerySqlGenerator Create()
        => new MySqlQuerySqlGenerator(dependencies, dialect, typeMappings, options.ReverseNullOrdering);
}

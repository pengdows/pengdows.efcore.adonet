using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet;

/// <summary>What a grammar part may write: text, identifiers, and any SQL or table expression rendered by the query generator.</summary>
public interface IQuerySqlWriter
{
    /// <summary>Appends SQL text to the statement being written.</summary>
    IQuerySqlWriter Append(string sql);
    /// <summary>Appends a line break to the statement being written.</summary>
    IQuerySqlWriter AppendLine();

    /// <summary>A delimited identifier; the schema is dropped when the dialect has none.</summary>
    IQuerySqlWriter AppendIdentifier(string name, string? schema = null);

    /// <summary>Writes an expression into the statement being written.</summary>
    IQuerySqlWriter Visit(Expression expression);

    /// <summary>Until disposed, columns of <paramref name="table" /> are qualified by its name instead of its alias.</summary>
    IDisposable QualifyWithTableName(TableExpression table);
}

/// <summary>How row limits and offsets are written. Subclass for databases with their own form (FIRST/SKIP, ROWNUM, ...).</summary>
public abstract class PagingSyntax
{
    /// <summary><c>OFFSET n ROWS FETCH NEXT m ROWS ONLY</c> (SQL:2008).</summary>
    public static PagingSyntax OffsetFetch { get; } = new OffsetFetchPaging();

    /// <summary>
    ///     <c>SELECT TOP(m)</c> without an offset, otherwise <c>OFFSET n ROWS FETCH NEXT m ROWS ONLY</c> with
    ///     <c>ORDER BY (SELECT 1)</c> when the query has no ordering, for databases whose OFFSET belongs to ORDER BY.
    /// </summary>
    public static PagingSyntax TopOrOffsetFetch { get; } = new TopOrOffsetFetchPaging();

    /// <summary>
    ///     <c>LIMIT m OFFSET n</c>. <paramref name="noLimitValue" /> is the LIMIT meaning "no limit", for databases that reject
    ///     OFFSET without LIMIT; null emits a bare OFFSET.
    /// </summary>
    public static PagingSyntax LimitOffset(string? noLimitValue = null)
        => new LimitOffsetPaging(noLimitValue);

    /// <summary>When true, OFFSET/LIMIT values are written as literals once known; the SQL is then not cached.</summary>
    public virtual bool InlinesValues { get; private set; }

    // Some offset/fetch grammars reject FETCH NEXT 0. A TOP form can represent the same empty result, but only after
    // the runtime paging parameter has been inspected. Keep this implementation detail internal so custom paging
    // syntaxes retain the existing parameter/cache behavior unless they explicitly opt into literal paging.
    internal virtual bool RequiresZeroLimitInlining => false;

    /// <summary>A copy that writes paging values as literals, for databases that reject parameters there.</summary>
    public virtual PagingSyntax WithLiteralValues()
    {
        var copy = (PagingSyntax)MemberwiseClone();
        copy.InlinesValues = true;
        return copy;
    }

    /// <summary>
    ///     False where a limit must be a value (a constant or parameter), not an expression: <c>Take(a).Take(b)</c> is then a
    ///     subquery, where it would otherwise be limited to <c>LEAST(a, b)</c>.
    /// </summary>
    public virtual bool ComputesLimits { get; private set; } = true;

    /// <summary>A copy for databases whose limit takes only a constant or parameter.</summary>
    public virtual PagingSyntax WithValueLimitsOnly()
    {
        var copy = (PagingSyntax)MemberwiseClone();
        copy.ComputesLimits = false;
        return copy;
    }

    /// <summary>Written right after <c>SELECT</c> (and <c>DISTINCT</c>).</summary>
    public virtual void GenerateTop(IQuerySqlWriter sql, SelectExpression select)
    {
    }

    /// <summary>Written after the ORDER BY clause, or where it would be.</summary>
    public virtual void GenerateAfterOrderings(IQuerySqlWriter sql, SelectExpression select)
    {
    }

    /// <summary>Written at the end of the query.</summary>
    public abstract void GenerateLimitOffset(IQuerySqlWriter sql, SelectExpression select);

    private class OffsetFetchPaging : PagingSyntax
    {
        public override void GenerateLimitOffset(IQuerySqlWriter sql, SelectExpression select)
        {
            if (select.Offset != null)
            {
                sql.AppendLine().Append("OFFSET ").Visit(select.Offset).Append(" ROWS");
                if (select.Limit != null)
                {
                    sql.Append(" FETCH NEXT ").Visit(select.Limit).Append(" ROWS ONLY");
                }
            }
            else if (select.Limit != null)
            {
                sql.AppendLine().Append("FETCH FIRST ").Visit(select.Limit).Append(" ROWS ONLY");
            }
        }
    }

    private sealed class TopOrOffsetFetchPaging : OffsetFetchPaging
    {
        internal override bool RequiresZeroLimitInlining => true;

        public override void GenerateTop(IQuerySqlWriter sql, SelectExpression select)
        {
            if (select is { Limit: { } limit } && (select.Offset == null || IsZeroLimit(limit)))
            {
                sql.Append("TOP(").Visit(limit).Append(") ");
            }
        }

        // OFFSET/FETCH is part of ORDER BY here.
        public override void GenerateAfterOrderings(IQuerySqlWriter sql, SelectExpression select)
        {
            if (select is { Orderings.Count: 0, Offset: not null })
            {
                sql.AppendLine().Append("ORDER BY (SELECT 1)");
            }
        }

        // A limit without an offset was written as TOP.
        public override void GenerateLimitOffset(IQuerySqlWriter sql, SelectExpression select)
        {
            if (select.Offset != null && !IsZeroLimit(select.Limit))
            {
                base.GenerateLimitOffset(sql, select);
            }
        }

        private static bool IsZeroLimit(SqlExpression? limit)
            => limit is SqlConstantExpression { Value: int value } && value == 0;
    }

    private sealed class LimitOffsetPaging(string? noLimitValue) : PagingSyntax
    {
        public override void GenerateLimitOffset(IQuerySqlWriter sql, SelectExpression select)
        {
            if (select.Limit != null)
            {
                sql.AppendLine().Append("LIMIT ").Visit(select.Limit);
            }
            else if (select.Offset != null && noLimitValue != null)
            {
                sql.AppendLine().Append("LIMIT ").Append(noLimitValue);
            }

            if (select.Offset != null)
            {
                sql.AppendLine().Append("OFFSET ").Visit(select.Offset);
            }
        }
    }
}

/// <summary>How bool values and conditions relate.</summary>
public abstract class BooleanSyntax
{
    /// <summary>Bool values and conditions are interchangeable: a bool column can be a predicate, a comparison can be projected.</summary>
    public static BooleanSyntax Native { get; } = new NativeBooleans();

    /// <summary>
    ///     Bools are values only, stored as bits: a value used as a predicate becomes <c>x = TRUE</c>, and a condition used
    ///     as a value becomes <c>CASE WHEN ... THEN TRUE ELSE FALSE END</c>, written with the dialect's TRUE/FALSE literals.
    /// </summary>
    public static BooleanSyntax BitValues { get; } = new BitValueBooleans(bitwiseOperators: false);

    /// <summary>
    ///     <see cref="BitValues" />, for a database with bitwise operators on them: a comparison of bool values
    ///     used as a value is <c>a ^ b</c> or <c>~(a ^ b)</c>, and a negated one <c>~a</c>, rather than a <c>CASE</c>.
    /// </summary>
    public static BooleanSyntax BitValuesWithBitwiseOperators { get; } = new BitValueBooleans(bitwiseOperators: true);

    /// <summary>Rewrites the SQL tree (a SELECT, or a bulk UPDATE/DELETE) once null semantics have been applied.</summary>
    public abstract Expression Process(Expression query, ISqlExpressionFactory sqlExpressionFactory);

    private sealed class NativeBooleans : BooleanSyntax
    {
        public override Expression Process(Expression query, ISqlExpressionFactory sqlExpressionFactory)
            => query;
    }

    private sealed class BitValueBooleans(bool bitwiseOperators) : BooleanSyntax
    {
        public override Expression Process(Expression query, ISqlExpressionFactory sqlExpressionFactory)
            => new AdoNetSearchConditionConverter(sqlExpressionFactory, bitwiseOperators).Visit(query);
    }
}

/// <summary>
///     A database's savepoints, written as SQL for drivers whose transactions don't offer them
///     (<see cref="System.Data.Common.DbTransaction.SupportsSavepoints" /> false). <c>{0}</c> is the delimited savepoint name.
/// </summary>
/// <param name="create">Sets a savepoint.</param>
/// <param name="rollbackTo">Undoes the work since it.</param>
/// <param name="release">Forgets it; null where savepoints last until the transaction ends.</param>
public sealed class SavepointSyntax(string create, string rollbackTo, string? release)
{
    /// <summary><c>SAVEPOINT</c>, <c>ROLLBACK TO SAVEPOINT</c>, <c>RELEASE SAVEPOINT</c> (SQL:1999).</summary>
    public static SavepointSyntax Iso { get; } = new("SAVEPOINT {0}", "ROLLBACK TO SAVEPOINT {0}", "RELEASE SAVEPOINT {0}");

    /// <summary>The statement that creates a savepoint with the given name.</summary>
    public string Create { get; } = create;
    /// <summary>The statement that rolls back to a savepoint.</summary>
    public string RollbackTo { get; } = rollbackTo;
    /// <summary>The statement that releases a savepoint.</summary>
    public string? Release { get; } = release;
}

/// <summary>
///     Statements around an INSERT that writes a key the application set into a column the database generates, for a database
///     that refuses one otherwise; <c>{0}</c> is the delimited table. Each insert is wrapped on its own, so a batch writing
///     several tables never has two switched on at once.
/// </summary>
/// <param name="enable">Run before the insert.</param>
/// <param name="disable">Run after it.</param>
public sealed class IdentityInsertSyntax(string enable, string disable)
{
    /// <summary>The statement that lets an explicit value be inserted into an identity column.</summary>
    public string Enable { get; } = enable;
    /// <summary>The statement that stops explicit values being inserted into an identity column.</summary>
    public string Disable { get; } = disable;
}

/// <summary>
///     How a subquery correlated with the rows before it is joined: what EF calls <c>CROSS APPLY</c> and <c>OUTER APPLY</c>.
/// </summary>
/// <param name="crossJoin">Written before the joined table when every row needs a match.</param>
/// <param name="outerJoin">Written before the joined table when a row without a match is kept.</param>
/// <param name="lateral">
///     Written after the join before a subquery or table function, which may then refer to the rows before it; a plain table
///     can't, and takes none.
/// </param>
/// <param name="outerJoinCondition">Written after the outer join's table, where the join needs a condition.</param>
public sealed class LateralJoinSyntax(string crossJoin, string outerJoin, string lateral, string? outerJoinCondition)
{
    /// <summary><c>CROSS JOIN LATERAL</c>, <c>LEFT JOIN LATERAL ... ON TRUE</c> (SQL:1999).</summary>
    public static LateralJoinSyntax Iso { get; } = new("CROSS JOIN ", "LEFT JOIN ", "LATERAL ", " ON TRUE");

    /// <summary><c>CROSS APPLY</c>, <c>OUTER APPLY</c>.</summary>
    public static LateralJoinSyntax Apply { get; } = new("CROSS APPLY ", "OUTER APPLY ", "", null);

    /// <summary>The join that pairs each row with the rows of a lateral subquery.</summary>
    public string CrossJoin { get; } = crossJoin;
    /// <summary>The join that keeps every row, pairing it with the rows of a lateral subquery when there are any.</summary>
    public string OuterJoin { get; } = outerJoin;
    /// <summary>The lateral-subquery form of a join's right side.</summary>
    public string Lateral { get; } = lateral;
    /// <summary>The condition of an outer lateral join, which is always true.</summary>
    public string? OuterJoinCondition { get; } = outerJoinCondition;
}

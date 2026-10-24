using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet;

/// <summary>What a grammar part may write: text, identifiers, and any SQL or table expression rendered by the query generator.</summary>
public interface IQuerySqlWriter
{
    IQuerySqlWriter Append(string sql);
    IQuerySqlWriter AppendLine();

    /// <summary>A delimited identifier; the schema is dropped when the dialect has none.</summary>
    IQuerySqlWriter AppendIdentifier(string name, string? schema = null);

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
    ///     <c>ORDER BY (SELECT 1)</c> when the query has no ordering (SQL Server).
    /// </summary>
    public static PagingSyntax TopOrOffsetFetch { get; } = new TopOrOffsetFetchPaging();

    /// <summary>
    ///     <c>LIMIT m OFFSET n</c>. <paramref name="noLimitValue" /> is the LIMIT meaning "no limit", for databases that reject
    ///     OFFSET without LIMIT; null emits a bare OFFSET.
    /// </summary>
    public static PagingSyntax LimitOffset(string? noLimitValue = null)
        => new LimitOffsetPaging(noLimitValue);

    /// <summary>When true, OFFSET/LIMIT values are written as literals once known; the SQL is then not cached.</summary>
    public bool InlinesValues { get; private set; }

    /// <summary>A copy that writes paging values as literals, for databases that reject parameters there.</summary>
    public PagingSyntax WithLiteralValues()
    {
        var copy = (PagingSyntax)MemberwiseClone();
        copy.InlinesValues = true;
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
        public override void GenerateTop(IQuerySqlWriter sql, SelectExpression select)
        {
            if (select is { Limit: { } limit, Offset: null })
            {
                sql.Append("TOP(").Visit(limit).Append(") ");
            }
        }

        // OFFSET/FETCH is part of ORDER BY on SQL Server.
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
            if (select.Offset != null)
            {
                base.GenerateLimitOffset(sql, select);
            }
        }
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
    ///     Bools are values only, like SQL Server's BIT: a value used as a predicate becomes <c>x = TRUE</c>, and a condition used
    ///     as a value becomes <c>CASE WHEN ... THEN TRUE ELSE FALSE END</c>, written with the dialect's TRUE/FALSE literals.
    /// </summary>
    public static BooleanSyntax BitValues { get; } = new BitValueBooleans();

    /// <summary>Rewrites the SQL tree (a SELECT, or a bulk UPDATE/DELETE) once null semantics have been applied.</summary>
    public abstract Expression Process(Expression query, ISqlExpressionFactory sqlExpressionFactory);

    private sealed class NativeBooleans : BooleanSyntax
    {
        public override Expression Process(Expression query, ISqlExpressionFactory sqlExpressionFactory)
            => query;
    }

    private sealed class BitValueBooleans : BooleanSyntax
    {
        public override Expression Process(Expression query, ISqlExpressionFactory sqlExpressionFactory)
            => new AdoNetSearchConditionConverter(sqlExpressionFactory).Visit(query);
    }
}

/// <summary>
///     How <c>ExecuteDelete</c> and <c>ExecuteUpdate</c> are written. Variants are named for their shape, not for a database.
/// </summary>
public abstract class DmlSyntax
{
    /// <summary>EF Core's relational SQL: <c>DELETE FROM t AS a</c>, <c>UPDATE t AS a SET c = v FROM other</c>.</summary>
    public static DmlSyntax Standard { get; } = new StandardDml();

    /// <summary>
    ///     Strict ISO: the target takes no alias, so its columns are qualified by table name; <c>UPDATE</c> through a join is not
    ///     supported.
    /// </summary>
    public static DmlSyntax UnaliasedTarget { get; } = new UnaliasedTargetDml();

    /// <summary><c>DELETE FROM a FROM t AS a JOIN ...</c>, <c>UPDATE a SET a.c = v FROM t AS a JOIN ...</c>.</summary>
    public static DmlSyntax TargetAliasFrom { get; } = new TargetAliasFromDml();

    /// <summary><c>DELETE a FROM t AS a JOIN ...</c>, <c>UPDATE t AS a JOIN ... SET a.c = v</c>.</summary>
    public static DmlSyntax MultiTable { get; } = new MultiTableDml();

    /// <summary>
    ///     Whether a DELETE over <paramref name="select" /> can be written as-is; when false, EF rewrites it as
    ///     <c>WHERE key IN (subquery)</c>. Null keeps EF's rule (a single table and a predicate).
    /// </summary>
    public virtual bool? CanDeleteDirectly(SelectExpression select)
        => null;

    /// <summary>Writes the DELETE, or returns false for EF's SQL.</summary>
    public virtual bool TryGenerateDelete(IQuerySqlWriter sql, DeleteExpression delete)
        => false;

    /// <summary>Writes the UPDATE, or returns false for EF's SQL.</summary>
    public virtual bool TryGenerateUpdate(IQuerySqlWriter sql, UpdateExpression update)
        => false;

    /// <summary>The selects that <c>FROM t AS a JOIN ...</c> forms can write: tables and joins, filtered, nothing else.</summary>
    protected static bool IsJoinsAndPredicateOnly(SelectExpression select)
        => select is { Tables: [TableExpression, ..], Orderings: [], Offset: null, Limit: null, GroupBy: [], Having: null, IsDistinct: false }
            && select.Tables.Skip(1).All(t => t is JoinExpressionBase);

    protected static void AppendTables(IQuerySqlWriter sql, SelectExpression select)
    {
        for (var i = 0; i < select.Tables.Count; i++)
        {
            if (i > 0)
            {
                sql.AppendLine();
            }

            sql.Visit(select.Tables[i]);
        }
    }

    protected static void AppendSetters(IQuerySqlWriter sql, UpdateExpression update, bool qualified)
    {
        sql.AppendLine().Append("SET ");
        for (var i = 0; i < update.ColumnValueSetters.Count; i++)
        {
            if (i > 0)
            {
                sql.Append(", ");
            }

            var (column, value) = update.ColumnValueSetters[i];
            if (qualified)
            {
                sql.Visit(column);
            }
            else
            {
                sql.AppendIdentifier(column.Name);
            }

            sql.Append(" = ").Visit(value);
        }
    }

    protected static void AppendWhere(IQuerySqlWriter sql, SelectExpression select)
    {
        if (select.Predicate != null)
        {
            sql.AppendLine().Append("WHERE ").Visit(select.Predicate);
        }
    }

    private sealed class StandardDml : DmlSyntax;

    private sealed class UnaliasedTargetDml : DmlSyntax
    {
        public override bool TryGenerateDelete(IQuerySqlWriter sql, DeleteExpression delete)
        {
            using (sql.QualifyWithTableName(delete.Table))
            {
                sql.Append("DELETE FROM ").AppendIdentifier(delete.Table.Name, delete.Table.Schema);
                AppendWhere(sql, delete.SelectExpression);
            }

            return true;
        }

        public override bool TryGenerateUpdate(IQuerySqlWriter sql, UpdateExpression update)
        {
            if (update.SelectExpression.Tables.Count > 1)
            {
                throw new NotSupportedException(
                    "ExecuteUpdate through a join needs an UPDATE target alias, which this dialect's SQL doesn't have.");
            }

            using (sql.QualifyWithTableName(update.Table))
            {
                sql.Append("UPDATE ").AppendIdentifier(update.Table.Name, update.Table.Schema);
                AppendSetters(sql, update, qualified: false);
                AppendWhere(sql, update.SelectExpression);
            }

            return true;
        }
    }

    private sealed class TargetAliasFromDml : DmlSyntax
    {
        public override bool? CanDeleteDirectly(SelectExpression select)
            => IsJoinsAndPredicateOnly(select);

        public override bool TryGenerateDelete(IQuerySqlWriter sql, DeleteExpression delete)
        {
            sql.Append("DELETE FROM ").AppendIdentifier(delete.Table.Alias).AppendLine().Append("FROM ");
            AppendTables(sql, delete.SelectExpression);
            AppendWhere(sql, delete.SelectExpression);
            return true;
        }

        public override bool TryGenerateUpdate(IQuerySqlWriter sql, UpdateExpression update)
        {
            sql.Append("UPDATE ").AppendIdentifier(update.Table.Alias);
            AppendSetters(sql, update, qualified: true);
            sql.AppendLine().Append("FROM ");
            AppendTables(sql, update.SelectExpression);
            AppendWhere(sql, update.SelectExpression);
            return true;
        }
    }

    private sealed class MultiTableDml : DmlSyntax
    {
        public override bool? CanDeleteDirectly(SelectExpression select)
            => IsJoinsAndPredicateOnly(select);

        public override bool TryGenerateDelete(IQuerySqlWriter sql, DeleteExpression delete)
        {
            sql.Append("DELETE ").AppendIdentifier(delete.Table.Alias).AppendLine().Append("FROM ");
            AppendTables(sql, delete.SelectExpression);
            AppendWhere(sql, delete.SelectExpression);
            return true;
        }

        public override bool TryGenerateUpdate(IQuerySqlWriter sql, UpdateExpression update)
        {
            sql.Append("UPDATE ");
            AppendTables(sql, update.SelectExpression);
            AppendSetters(sql, update, qualified: true);
            AppendWhere(sql, update.SelectExpression);
            return true;
        }
    }
}

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

    public string Create { get; } = create;
    public string RollbackTo { get; } = rollbackTo;
    public string? Release { get; } = release;
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

    public string CrossJoin { get; } = crossJoin;
    public string OuterJoin { get; } = outerJoin;
    public string Lateral { get; } = lateral;
    public string? OuterJoinCondition { get; } = outerJoinCondition;
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

/// <summary>
///     JSON columns (owned and complex types mapped with <c>ToJson</c>): the column's type, and how a value inside a document
///     is read and patched. <c>{0}</c> is the JSON column or expression, <c>{1}</c> the SQL/JSON path literal (<c>'$.a[0].b'</c>),
///     <c>{2}</c> the store type a value is returned as, or the new value. Each reading template is a function call, so it is
///     never parenthesized as an operand.
/// </summary>
/// <param name="storeType">The JSON column's type.</param>
/// <param name="value">A string value inside the document.</param>
/// <param name="query">A sub-document (an object or array), as JSON.</param>
/// <param name="typedValue">A value inside the document returned as the store type <c>{2}</c>.</param>
/// <param name="booleanValue">A boolean value inside the document, where a typed value can't express it.</param>
/// <param name="concatenate">Joins two strings, to build a path whose array index is computed: <c>{0} || {1}</c>.</param>
/// <param name="set">Replaces the value at the path <c>{1}</c> in <c>{0}</c> with <c>{2}</c>.</param>
/// <param name="fromText">Turns JSON text (a sub-document, or <c>true</c>/<c>false</c>) into a JSON value.</param>
public sealed class JsonSyntax(
    string storeType,
    string value,
    string query,
    string typedValue,
    string? booleanValue,
    string concatenate,
    string set,
    string fromText)
{
    /// <summary>
    ///     SQL:2016's JSON_VALUE and JSON_QUERY, SQL:2023's JSON type and JSON() constructor, and JSON_SET, which ISO lacks and
    ///     several databases share.
    /// </summary>
    public static JsonSyntax Iso { get; } = new(
        "JSON",
        "JSON_VALUE({0}, {1})",
        "JSON_QUERY({0}, {1})",
        "JSON_VALUE({0}, {1} RETURNING {2})",
        booleanValue: null,
        "{0} || {1}",
        "JSON_SET({0}, {1}, {2})",
        "JSON({0})");

    public string StoreType { get; } = storeType;
    public string Value { get; } = value;
    public string Query { get; } = query;
    public string TypedValue { get; } = typedValue;
    public string? BooleanValue { get; } = booleanValue;
    public string Concatenate { get; } = concatenate;
    public string Set { get; } = set;
    public string FromText { get; } = fromText;
}

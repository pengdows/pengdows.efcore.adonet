using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using pengdows.efcore.adonet.Query;
namespace pengdows.efcore.adonet;

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
    ///     ISO SQL:2003, which has no <c>UPDATE ... FROM</c>: the target takes its correlation name, and an UPDATE through a
    ///     join keeps the target alone, the joined tables in a correlated <c>WHERE EXISTS (SELECT 1 FROM ...)</c>, and a value
    ///     read from them in a correlated scalar subquery over the same tables.
    /// </summary>
    public static DmlSyntax Iso { get; } = new IsoDml();

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

    // Ordering and DISTINCT do not change the set of rows affected by ExecuteDelete. Some servers reject the fallback
    // key-IN-subquery form when that subquery reads the target table, so a single-table delete must stay direct.
    private static bool IsSingleTableDelete(SelectExpression select)
        => select is { Tables: [TableExpression], Offset: null, Limit: null, GroupBy: [], Having: null };

    /// <summary>Writes the tables an UPDATE or DELETE names.</summary>
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

    /// <summary>Writes the SET clause of an UPDATE.</summary>
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

    /// <summary>Writes the WHERE clause of an UPDATE or DELETE.</summary>
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

    private sealed class IsoDml : DmlSyntax
    {
        public override bool TryGenerateUpdate(IQuerySqlWriter sql, UpdateExpression update)
        {
            var select = update.SelectExpression;
            if (select.Tables.Count == 1)
            {
                return false;
            }

            if (!IsJoinsAndPredicateOnly(select))
            {
                throw new NotSupportedException("ExecuteUpdate through a join is written for ISO SQL only over tables and inner joins, filtered.");
            }

            // The tables other than the target, and every condition: the joins' and the query's.
            var others = new List<TableExpressionBase>();
            var conditions = new List<SqlExpression>();
            foreach (var table in select.Tables)
            {
                var (source, condition) = table switch
                {
                    InnerJoinExpression inner => (inner.Table, inner.JoinPredicate),
                    CrossJoinExpression cross => (cross.Table, (SqlExpression?)null),
                    JoinExpressionBase => throw new NotSupportedException(
                        "ExecuteUpdate through an outer join can't be written in ISO SQL: it has no UPDATE ... FROM."),
                    _ => (table, null)
                };
                if (condition != null)
                {
                    conditions.Add(condition);
                }

                if (source.Alias != update.Table.Alias)
                {
                    others.Add(source);
                }
            }

            if (select.Predicate != null)
            {
                conditions.Add(select.Predicate);
            }

            var otherAliases = others.Select(t => t.Alias!).ToHashSet(StringComparer.Ordinal);
            sql.Append("UPDATE ").AppendIdentifier(update.Table.Name, update.Table.Schema)
                .Append(" AS ").AppendIdentifier(update.Table.Alias!);
            sql.AppendLine().Append("SET ");
            for (var i = 0; i < update.ColumnValueSetters.Count; i++)
            {
                if (i > 0)
                {
                    sql.Append(", ");
                }

                var (column, value) = update.ColumnValueSetters[i];
                sql.AppendIdentifier(column.Name).Append(" = ");
                if (ColumnAliases.Of(value).Overlaps(otherAliases))
                {
                    sql.Append("(SELECT ").Visit(value);
                    AppendFromWhere(sql, others, conditions);
                    sql.Append(")");
                }
                else
                {
                    sql.Visit(value);
                }
            }

            sql.AppendLine().Append("WHERE EXISTS (SELECT 1");
            AppendFromWhere(sql, others, conditions);
            sql.Append(")");
            return true;
        }

        private static void AppendFromWhere(IQuerySqlWriter sql, IReadOnlyList<TableExpressionBase> tables, IReadOnlyList<SqlExpression> conditions)
        {
            sql.Append(" FROM ");
            for (var i = 0; i < tables.Count; i++)
            {
                if (i > 0)
                {
                    sql.Append(" CROSS JOIN ");
                }

                sql.Visit(tables[i]);
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                sql.Append(i == 0 ? " WHERE (" : " AND (").Visit(conditions[i]).Append(")");
            }
        }

        /// <summary>The aliases of the tables whose columns an expression reads.</summary>
        private sealed class ColumnAliases : ExpressionVisitor
        {
            private readonly HashSet<string> _aliases = new(StringComparer.Ordinal);

            public static HashSet<string> Of(Expression expression)
            {
                var visitor = new ColumnAliases();
                visitor.Visit(expression);
                return visitor._aliases;
            }

            protected override Expression VisitExtension(Expression node)
            {
                if (node is ColumnExpression column)
                {
                    _aliases.Add(column.TableAlias);
                    return node;
                }

                return base.VisitExtension(node);
            }
        }
    }

    private sealed class MultiTableDml : DmlSyntax
    {
        public override bool? CanDeleteDirectly(SelectExpression select)
            => IsJoinsAndPredicateOnly(select) || IsSingleTableDelete(select);

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

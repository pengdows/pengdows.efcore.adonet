using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

public class AdoNetQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies, AdoNetSqlDialect dialect)
    : QuerySqlGenerator(dependencies)
{
    protected override void GenerateTop(SelectExpression selectExpression)
    {
        if (dialect.Paging == PagingStyle.TopOrOffsetFetch && selectExpression is { Limit: { } limit, Offset: null })
        {
            Sql.Append("TOP(");
            Visit(limit);
            Sql.Append(") ");
        }
    }

    protected override void GenerateOrderings(SelectExpression selectExpression)
    {
        base.GenerateOrderings(selectExpression);

        // OFFSET/FETCH is part of ORDER BY on SQL Server.
        if (dialect.Paging == PagingStyle.TopOrOffsetFetch
            && selectExpression is { Orderings.Count: 0, Offset: not null })
        {
            Sql.AppendLine().Append("ORDER BY (SELECT 1)");
        }
    }

    protected override void GenerateLimitOffset(SelectExpression selectExpression)
    {
        if (dialect.Paging == PagingStyle.TopOrOffsetFetch)
        {
            // A limit without an offset was written as TOP.
            if (selectExpression.Offset != null)
            {
                base.GenerateLimitOffset(selectExpression);
            }

            return;
        }

        if (dialect.Paging == PagingStyle.OffsetFetch)
        {
            base.GenerateLimitOffset(selectExpression);
            return;
        }

        if (selectExpression.Limit == null && selectExpression.Offset == null)
        {
            return;
        }

        if (selectExpression.Limit != null)
        {
            Sql.AppendLine().Append("LIMIT ");
            Visit(selectExpression.Limit);
        }
        else if (dialect.NoLimitValue != null)
        {
            Sql.AppendLine().Append("LIMIT ").Append(dialect.NoLimitValue);
        }

        if (selectExpression.Offset != null)
        {
            Sql.AppendLine().Append("OFFSET ");
            Visit(selectExpression.Offset);
        }
    }

    protected override Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression)
    {
        if (sqlFunctionExpression is { IsBuiltIn: true, Name: "COUNT", Instance: null, Arguments: { } arguments }
            && sqlFunctionExpression.Type == typeof(long)
            && dialect.LongCountFunction != "COUNT")
        {
            Sql.Append(dialect.LongCountFunction).Append("(");
            for (var i = 0; i < arguments.Count; i++)
            {
                if (i > 0)
                {
                    Sql.Append(", ");
                }

                Visit(arguments[i]);
            }

            Sql.Append(")");
            return sqlFunctionExpression;
        }

        return base.VisitSqlFunction(sqlFunctionExpression);
    }
}

public class AdoNetQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies, AdoNetSqlDialect dialect)
    : IQuerySqlGeneratorFactory
{
    public virtual QuerySqlGenerator Create()
        => new AdoNetQuerySqlGenerator(dependencies, dialect);
}

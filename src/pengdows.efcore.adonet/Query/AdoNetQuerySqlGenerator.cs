using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

public class AdoNetQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies, AdoNetSqlDialect dialect)
    : QuerySqlGenerator(dependencies)
{
    protected override void GenerateLimitOffset(SelectExpression selectExpression)
    {
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
}

public class AdoNetQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies, AdoNetSqlDialect dialect)
    : IQuerySqlGeneratorFactory
{
    public virtual QuerySqlGenerator Create()
        => new AdoNetQuerySqlGenerator(dependencies, dialect);
}

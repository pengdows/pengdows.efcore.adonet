using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational SQL, with paging, <c>LongCount</c> and bulk DML as the dialect writes them.</summary>
public class AdoNetQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies, ISqlDialect dialect)
    : QuerySqlGenerator(dependencies), IQuerySqlWriter
{
    private TableExpression? _qualifyWithTableName;

    protected override Expression VisitDelete(DeleteExpression deleteExpression)
        => dialect.Dml.TryGenerateDelete(this, deleteExpression) ? deleteExpression : base.VisitDelete(deleteExpression);

    protected override Expression VisitUpdate(UpdateExpression updateExpression)
        => dialect.Dml.TryGenerateUpdate(this, updateExpression) ? updateExpression : base.VisitUpdate(updateExpression);

    protected override Expression VisitColumn(ColumnExpression columnExpression)
    {
        if (_qualifyWithTableName is { } table && columnExpression.TableAlias == table.Alias)
        {
            Sql.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table.Name, table.Schema))
                .Append(".")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(columnExpression.Name));
            return columnExpression;
        }

        return base.VisitColumn(columnExpression);
    }

    protected override void GenerateTop(SelectExpression selectExpression)
        => dialect.Paging.GenerateTop(this, selectExpression);

    protected override void GenerateOrderings(SelectExpression selectExpression)
    {
        base.GenerateOrderings(selectExpression);
        dialect.Paging.GenerateAfterOrderings(this, selectExpression);
    }

    protected override void GenerateLimitOffset(SelectExpression selectExpression)
        => dialect.Paging.GenerateLimitOffset(this, selectExpression);

    protected override Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression)
    {
        // EF's COUNT always has exactly one argument: * or an expression.
        if (sqlFunctionExpression is { IsBuiltIn: true, Name: "COUNT", Instance: null, Arguments: [var argument] }
            && sqlFunctionExpression.Type == typeof(long)
            && dialect.LongCountFunction != "COUNT")
        {
            Sql.Append(dialect.LongCountFunction).Append("(");
            Visit(argument);
            Sql.Append(")");
            return sqlFunctionExpression;
        }

        return base.VisitSqlFunction(sqlFunctionExpression);
    }

    IQuerySqlWriter IQuerySqlWriter.Append(string sql)
    {
        Sql.Append(sql);
        return this;
    }

    IQuerySqlWriter IQuerySqlWriter.AppendLine()
    {
        Sql.AppendLine();
        return this;
    }

    IQuerySqlWriter IQuerySqlWriter.AppendIdentifier(string name, string? schema)
    {
        Sql.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name, schema));
        return this;
    }

    IQuerySqlWriter IQuerySqlWriter.Visit(Expression expression)
    {
        Visit(expression);
        return this;
    }

    IDisposable IQuerySqlWriter.QualifyWithTableName(TableExpression table)
    {
        var previous = _qualifyWithTableName;
        _qualifyWithTableName = table;
        return new Restore(() => _qualifyWithTableName = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose()
            => restore();
    }
}

public class AdoNetQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies, ISqlDialect dialect)
    : IQuerySqlGeneratorFactory
{
    public virtual QuerySqlGenerator Create()
        => new AdoNetQuerySqlGenerator(dependencies, dialect);
}

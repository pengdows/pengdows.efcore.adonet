using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational SQL, with paging, functions, <c>LongCount</c> and bulk DML as the dialect writes them.</summary>
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

    protected override Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression)
    {
        if (sqlBinaryExpression is { OperatorType: ExpressionType.Add } && sqlBinaryExpression.Type == typeof(string))
        {
            SqlTemplate.Write(
                dialect.Functions.Concat, [sqlBinaryExpression.Left, sqlBinaryExpression.Right], s => Sql.Append(s), e => Visit(e));
            return sqlBinaryExpression;
        }

        return base.VisitSqlBinary(sqlBinaryExpression);
    }

    // SQL's standard precedence, highest first (vendors document the same order), so only what needs them gets parentheses.
    protected override bool TryGetOperatorInfo(SqlExpression expression, out int precedence, out bool isAssociative)
    {
        (precedence, isAssociative) = expression switch
        {
            SqlBinaryExpression binary => binary.OperatorType switch
            {
                ExpressionType.Multiply => (900, true),
                ExpressionType.Divide or ExpressionType.Modulo => (900, false),
                ExpressionType.Add => (700, true),
                ExpressionType.Subtract => (700, false),
                ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr => (700, true),
                ExpressionType.LessThan or ExpressionType.LessThanOrEqual or ExpressionType.GreaterThan
                    or ExpressionType.GreaterThanOrEqual or ExpressionType.Equal or ExpressionType.NotEqual => (500, false),
                ExpressionType.AndAlso => (200, true),
                ExpressionType.OrElse => (100, true),
                _ => default
            },
            SqlUnaryExpression unary => unary.OperatorType switch
            {
                ExpressionType.Convert => (1300, false),
                ExpressionType.OnesComplement => (1200, false),
                ExpressionType.Not when unary.Type != typeof(bool) => (1200, false),
                ExpressionType.Negate => (1100, false),
                ExpressionType.Equal or ExpressionType.NotEqual => (500, false),
                ExpressionType.Not => (300, false),
                _ => default
            },
            CollateExpression => (900, false),
            LikeExpression => (350, false),
            AtTimeZoneExpression => (1200, false),
            _ => default
        };

        return precedence != default;
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
        if (SqlTemplate.TryGetTemplate(sqlFunctionExpression, out var template))
        {
            SqlTemplate.Write(template, sqlFunctionExpression.Arguments!, s => Sql.Append(s), e => Visit(e));
            return sqlFunctionExpression;
        }

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

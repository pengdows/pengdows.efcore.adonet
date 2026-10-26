using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational LINQ translation; the dialect's <see cref="DmlSyntax" /> decides which deletes it writes directly.</summary>
internal class AdoNetQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
{
    private readonly ISqlDialect _dialect;
    private readonly IRelationalTypeMappingSource _typeMappingSource;
    private readonly SqlAliasManager _sqlAliasManager;
    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public AdoNetQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext,
        ISqlDialect dialect)
        : base(dependencies, relationalDependencies, queryCompilationContext)
    {
        _dialect = dialect;
        _typeMappingSource = relationalDependencies.TypeMappingSource;
        _sqlAliasManager = queryCompilationContext.SqlAliasManager;
        _sqlExpressionFactory = relationalDependencies.SqlExpressionFactory;
    }

    protected AdoNetQueryableMethodTranslatingExpressionVisitor(AdoNetQueryableMethodTranslatingExpressionVisitor parentVisitor)
        : base(parentVisitor)
    {
        _dialect = parentVisitor._dialect;
        _typeMappingSource = parentVisitor._typeMappingSource;
        _sqlAliasManager = parentVisitor._sqlAliasManager;
        _sqlExpressionFactory = parentVisitor._sqlExpressionFactory;
    }

    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new AdoNetQueryableMethodTranslatingExpressionVisitor(this);

    // A collection column, or a parameter collection that isn't expanded into an IN list, queried as the table of its JSON array's
    // elements, ordered as the array is. Adapted from EF Core's SQLite provider (MIT, .NET Foundation):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqliteQueryableMethodTranslatingExpressionVisitor.cs
    protected override ShapedQueryExpression? TranslatePrimitiveCollection(
        SqlExpression sqlExpression,
        Microsoft.EntityFrameworkCore.Metadata.IProperty? property,
        string tableAlias)
    {
        if (_dialect.Json?.ArrayElements == null)
        {
            return null;
        }

        var elementTypeMapping = (RelationalTypeMapping?)sqlExpression.TypeMapping?.ElementTypeMapping;
        var elementClrType = JsonArrayTableExpression.ElementType(sqlExpression.Type);
        var table = new JsonArrayTableExpression(tableAlias, sqlExpression, elementTypeMapping);

        // A collection column's element nullability is in the model; a parameter's isn't, so its CLR type decides.
        var isElementNullable = property?.GetElementType()!.IsNullable;
        var ordinalTypeMapping = _typeMappingSource.FindMapping(typeof(int))!;

#pragma warning disable EF1001 // EF's provider-facing SelectExpression constructor, marked internal like the rest of its provider API.
        var select = new SelectExpression(
            [table],
            new ColumnExpression(
                JsonArrayTableExpression.ValueColumn,
                tableAlias,
                System.Nullable.GetUnderlyingType(elementClrType) ?? elementClrType,
                elementTypeMapping,
                isElementNullable ?? (!elementClrType.IsValueType || (System.Nullable.GetUnderlyingType(elementClrType) != null))),
            identifier:
            [
                (new ColumnExpression(JsonArrayTableExpression.OrdinalColumn, tableAlias, typeof(int), ordinalTypeMapping, nullable: false),
                    ordinalTypeMapping.Comparer)
            ],
            _sqlAliasManager);
#pragma warning restore EF1001

        select.AppendOrdering(
            new OrderingExpression(
                select.CreateColumnExpression(table, JsonArrayTableExpression.OrdinalColumn, typeof(int), ordinalTypeMapping, columnNullable: false),
                ascending: true));

        Expression shaper = new ProjectionBindingExpression(select, new ProjectionMember(), Nullable(elementClrType));
        if (shaper.Type != elementClrType)
        {
            shaper = Expression.Convert(shaper, elementClrType);
        }

        return new ShapedQueryExpression(select, shaper);
    }

    // collection[i] is OFFSET i LIMIT 1 over the collection in order. Where OFFSET takes only a value and i is computed (a
    // column), an inline or JSON collection is filtered on its ordinal instead: VALUES numbers its rows from 0 (_ord), a JSON
    // array table from 1.
    protected override ShapedQueryExpression? TranslateElementAtOrDefault(ShapedQueryExpression source, Expression index, bool returnDefault)
    {
        if (!_dialect.Paging.ComputesLimits
            && (source.QueryExpression is SelectExpression
            {
                Tables: [var table],
                Orderings: [{ Expression: ColumnExpression ordinal, IsAscending: true }],
                Limit: null,
                Offset: null,
                IsDistinct: false
            } select)
            && (ordinal.TableAlias == table.Alias)
            && (table is ValuesExpression || ((table is JsonArrayTableExpression) && (ordinal.Name == JsonArrayTableExpression.OrdinalColumn)))
            && (TranslateExpression(index) is { } position and not (SqlConstantExpression or SqlParameterExpression)))
        {
            // VALUES' ordinal has no type mapping until the query's type mappings are inferred.
            var factory = _sqlExpressionFactory;
            var intMapping = ordinal.TypeMapping ?? _typeMappingSource.FindMapping(typeof(int))!;
            position = factory.ApplyTypeMapping(position, intMapping)!;
            select.ApplyPredicate(factory.Equal(
                ordinal.ApplyTypeMapping(intMapping),
                table is JsonArrayTableExpression ? factory.Add(position, factory.Constant(1, intMapping)) : position));
            select.ClearOrdering();
            select.ApplyLimit(factory.Constant(1, intMapping));
            return source;
        }

        return base.TranslateElementAtOrDefault(source, index, returnDefault);
    }

    // A JSON array table ordered by its ordinal is in its natural order, so Distinct after it isn't Distinct after an ordering.
    // Adapted from EF Core's SQLite provider (MIT, .NET Foundation).
    protected override bool IsNaturallyOrdered(SelectExpression selectExpression)
        => selectExpression is
            {
                Tables: [JsonArrayTableExpression table, ..],
                Orderings: [{ Expression: ColumnExpression { Name: JsonArrayTableExpression.OrdinalColumn } ordinal, IsAscending: true }]
            }
            && (ordinal.TableAlias == table.Alias);

    private static Type Nullable(Type type)
        => type.IsValueType && (System.Nullable.GetUnderlyingType(type) == null) ? typeof(Nullable<>).MakeGenericType(type) : type;

    protected override bool IsValidSelectExpressionForExecuteDelete(SelectExpression selectExpression)
        => _dialect.Dml.CanDeleteDirectly(selectExpression) ?? base.IsValidSelectExpressionForExecuteDelete(selectExpression);
}

internal class AdoNetQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
    ISqlDialect dialect)
    : IQueryableMethodTranslatingExpressionVisitorFactory
{
    public virtual QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => new AdoNetQueryableMethodTranslatingExpressionVisitor(
            dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext, dialect);
}

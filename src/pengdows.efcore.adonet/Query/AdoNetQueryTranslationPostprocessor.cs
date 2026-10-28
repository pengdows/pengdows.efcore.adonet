using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational postprocessing, with a type-mapping pass that knows the core's own SQL expressions.</summary>
internal sealed class AdoNetQueryTranslationPostprocessor(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    RelationalQueryCompilationContext queryCompilationContext,
    IAdoNetDialect dialect)
    : RelationalQueryTranslationPostprocessor(dependencies, relationalDependencies, queryCompilationContext)
{
    public override Expression Process(Expression query)
    {
        var processed = base.Process(query);
        if ((dialect.Sql.LateralJoins == null) && LateralJoinFinder.Finds(processed))
        {
            throw new LateralJoinNotSupportedException(
                $"This query joins a subquery correlated with the rows before it (a lateral join, EF's APPLY), which {dialect.Name} doesn't have.");
        }

        return processed;
    }

    protected override Expression ProcessTypeMappings(Expression expression)
        => new AdoNetTypeMappingPostprocessor(
            Dependencies, RelationalDependencies, RelationalQueryCompilationContext, dialect.Sql.Json?.ArrayElementsBinaryValue).Process(expression);
}

/// <summary>
///     A row value has no type mapping of its own: it is written from its values, each of which has one. A JSON array table
///     over a parameter takes its element type from how the query uses the elements, and the parameter is then serialized as a
///     JSON array of that type; the element columns read out of it take the same type.
///     Behaviour reference (not copied): EF Core's SQLite provider (MIT, .NET Foundation):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqliteTypeMappingPostprocessor.cs
/// </summary>
internal sealed class AdoNetTypeMappingPostprocessor(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    RelationalQueryCompilationContext queryCompilationContext,
    string? binaryValue)
    : RelationalTypeMappingPostprocessor(dependencies, relationalDependencies, queryCompilationContext)
{
    private readonly Microsoft.EntityFrameworkCore.Storage.IRelationalTypeMappingSource _typeMappingSource
        = relationalDependencies.TypeMappingSource;

    private readonly Microsoft.EntityFrameworkCore.Metadata.IModel _model = queryCompilationContext.Model;

    // The element type mappings of the JSON array tables in the SELECT being visited, by alias.
    private Dictionary<string, Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping>? _arrayTables;

    protected override Expression VisitExtension(Expression expression)
    {
        if (expression is RowValueSqlExpression rowValue)
        {
            return rowValue.Update(rowValue.Values.Select(value => (SqlExpression)Visit(value)).ToList());
        }

        if (expression is JsonArrayTableExpression jsonTable)
        {
            return WithElementType(jsonTable, FindElementMapping(jsonTable));
        }

        if (expression is SelectExpression select)
        {
            return VisitSelectWithArrayMappings(select);
        }

        if (expression is ColumnExpression { Name: JsonArrayTableExpression.ValueColumn } valueColumn
            && _arrayTables?.TryGetValue(valueColumn.TableAlias, out var elementMapping) is true)
        {
            var typedColumn = valueColumn.TypeMapping == null
                ? valueColumn.ApplyTypeMapping(elementMapping)
                : valueColumn;
            return elementMapping.ClrType == typeof(byte[]) && binaryValue != null
                ? RelationalDependencies.SqlExpressionFactory.Call(binaryValue, [typedColumn], typeof(byte[]), elementMapping)!
                : typedColumn;
        }

        return base.VisitExtension(expression);
    }

    private Expression VisitSelectWithArrayMappings(SelectExpression select)
    {
        var outerMappings = _arrayTables;
        var localMappings = outerMappings;
        foreach (var table in select.Tables.OfType<JsonArrayTableExpression>())
        {
            var mapping = FindElementMapping(table);
            if (mapping == null)
            {
                continue;
            }

            localMappings ??= new Dictionary<string, Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping>(outerMappings ?? []);
            localMappings[table.Alias] = mapping;
        }

        _arrayTables = localMappings;
        try
        {
            return base.VisitExtension(select);
        }
        finally
        {
            _arrayTables = outerMappings;
        }
    }

    // A column collection's element mapping comes from the model; a parameter's is inferred from its use, or else the default
    // for the element type.
    private Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping? FindElementMapping(JsonArrayTableExpression table)
        => table.ElementTypeMapping
            ?? (TryGetInferredTypeMapping(table.Alias, JsonArrayTableExpression.ValueColumn, out var inferred) ? inferred : null)
            ?? _typeMappingSource.FindMapping(JsonArrayTableExpression.ElementType(table.Json.Type), _model);

    private JsonArrayTableExpression WithElementType(
        JsonArrayTableExpression table,
        Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping? elementMapping)
    {
        // A parameter is mapped before it is visited: EF refuses one without a mapping.
        var json = table.Json is SqlParameterExpression { TypeMapping: null } parameter && (elementMapping != null)
            ? parameter.ApplyTypeMapping(_typeMappingSource.FindMapping(parameter.Type, _model, elementMapping))
            : table.Json;
        return table.Update((SqlExpression)Visit(json), elementMapping);
    }
}

/// <summary>Whether a translated query holds a CROSS or OUTER APPLY of a subquery: of a plain table, it is an ordinary join.</summary>
internal sealed class LateralJoinFinder : ExpressionVisitor
{
    private bool _found;

    public static bool Finds(Expression expression)
    {
        var finder = new LateralJoinFinder();
        finder.Visit(expression);
        return finder._found;
    }

    public override Expression? Visit(Expression? node)
    {
        if (_found)
        {
            return node;
        }

        if (node is CrossApplyExpression { Table: not TableExpression } or OuterApplyExpression { Table: not TableExpression })
        {
            _found = true;
            return node;
        }

        // A shaped query isn't visited as a whole: its query and its shaper (which holds collection subqueries) are.
        if (node is ShapedQueryExpression shaped)
        {
            Visit(shaped.QueryExpression);
            Visit(shaped.ShaperExpression);
            return node;
        }

        return base.Visit(node);
    }
}

/// <summary>A query that needs a lateral join, on a database that has none: the dialect's capability, not a translation bug.</summary>
internal sealed class LateralJoinNotSupportedException(string message) : InvalidOperationException(message);

internal sealed class AdoNetQueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    IAdoNetDialect dialect) : IQueryTranslationPostprocessorFactory
{
    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new AdoNetQueryTranslationPostprocessor(
            dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext, dialect);
}

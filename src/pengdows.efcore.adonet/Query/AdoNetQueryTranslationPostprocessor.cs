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
            throw new InvalidOperationException(
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
///     Adapted from EF Core's SQLite provider (MIT, .NET Foundation):
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
        switch (expression)
        {
            case RowValueSqlExpression rowValue:
                return rowValue.Update(rowValue.Values.Select(v => (SqlExpression)Visit(v)).ToList());

            case JsonArrayTableExpression table:
                return WithElementType(table, ElementTypeMapping(table));

            case SelectExpression select:
            {
                var outer = _arrayTables;
                foreach (var table in select.Tables.OfType<JsonArrayTableExpression>())
                {
                    if (ElementTypeMapping(table) is { } mapping)
                    {
                        _arrayTables = _arrayTables == outer ? new Dictionary<string, Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping>(outer ?? []) : _arrayTables;
                        _arrayTables![table.Alias] = mapping;
                    }
                }

                var visited = base.VisitExtension(expression);
                _arrayTables = outer;
                return visited;
            }

            case ColumnExpression { Name: JsonArrayTableExpression.ValueColumn } column
                when _arrayTables?.TryGetValue(column.TableAlias, out var elementMapping) is true:
            {
                var typed = column.TypeMapping == null ? column.ApplyTypeMapping(elementMapping) : column;
                return (elementMapping.ClrType == typeof(byte[])) && (binaryValue != null)
                    ? RelationalDependencies.SqlExpressionFactory.Call(binaryValue, [typed], typeof(byte[]), elementMapping)!
                    : typed;
            }

            default:
                return base.VisitExtension(expression);
        }
    }

    // A column collection's element mapping comes from the model; a parameter's is inferred from its use, or else the default
    // for the element type.
    private Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping? ElementTypeMapping(JsonArrayTableExpression table)
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

internal sealed class AdoNetQueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
    IAdoNetDialect dialect) : IQueryTranslationPostprocessorFactory
{
    public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new AdoNetQueryTranslationPostprocessor(
            dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext, dialect);
}

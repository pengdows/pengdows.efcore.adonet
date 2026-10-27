using System.Diagnostics;
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
    private readonly ISqlGenerationHelper _sqlGenerationHelper;

    public AdoNetQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext,
        ISqlDialect dialect,
        ISqlGenerationHelper sqlGenerationHelper)
        : base(dependencies, relationalDependencies, queryCompilationContext)
    {
        _dialect = dialect;
        _typeMappingSource = relationalDependencies.TypeMappingSource;
        _sqlAliasManager = queryCompilationContext.SqlAliasManager;
        _sqlExpressionFactory = relationalDependencies.SqlExpressionFactory;
        _sqlGenerationHelper = sqlGenerationHelper;
    }

    protected AdoNetQueryableMethodTranslatingExpressionVisitor(AdoNetQueryableMethodTranslatingExpressionVisitor parentVisitor)
        : base(parentVisitor)
    {
        _dialect = parentVisitor._dialect;
        _typeMappingSource = parentVisitor._typeMappingSource;
        _sqlAliasManager = parentVisitor._sqlAliasManager;
        _sqlExpressionFactory = parentVisitor._sqlExpressionFactory;
        _sqlGenerationHelper = parentVisitor._sqlGenerationHelper;
    }

    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new AdoNetQueryableMethodTranslatingExpressionVisitor(this);

    // A collection of structural types inside a JSON column, queried as a table: one row per element, ordered as the array is,
    // each property read from the element's JSON as it is from a JSON column. A collection at a path is read from there first.
    // Adapted from EF Core's SQLite provider (MIT, .NET Foundation):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqliteQueryableMethodTranslatingExpressionVisitor.cs
#pragma warning disable EF1001 // CreateSelect: EF's provider-facing helper for a JSON collection's select, marked internal like the rest of its provider API.
    protected override ShapedQueryExpression TransformJsonQueryToTable(JsonQueryExpression jsonQueryExpression)
    {
        if (_dialect.Json?.ArrayElements == null)
        {
            return base.TransformJsonQueryToTable(jsonQueryExpression)!;
        }

        var type = jsonQueryExpression.StructuralType;
        var jsonMapping = _typeMappingSource.FindMapping(typeof(JsonTypePlaceholder))!;
        var stringMapping = _typeMappingSource.FindMapping(typeof(string))!;
        var intMapping = _typeMappingSource.FindMapping(typeof(int))!;
        SqlExpression source = jsonQueryExpression.Path.Count == 0
            ? jsonQueryExpression.JsonColumn
            : new JsonScalarExpression(jsonQueryExpression.JsonColumn, jsonQueryExpression.Path, typeof(string), jsonMapping, jsonQueryExpression.IsNullable);

        var name = jsonQueryExpression.Path.LastOrDefault(p => p.PropertyName != null).PropertyName ?? jsonQueryExpression.JsonColumn.Name;
        var table = new JsonArrayTableExpression(_sqlAliasManager.GenerateTableAlias(name), source, jsonMapping);
        var select = CreateSelect(jsonQueryExpression, table, JsonArrayTableExpression.OrdinalColumn, typeof(int), intMapping);
        select.AppendOrdering(new OrderingExpression(
            select.CreateColumnExpression(table, JsonArrayTableExpression.OrdinalColumn, typeof(int), intMapping, columnNullable: false), ascending: true));

        var element = select.CreateColumnExpression(table, JsonArrayTableExpression.ValueColumn, typeof(string), jsonMapping, columnNullable: null);
        var projection = new Dictionary<ProjectionMember, Expression>();
        foreach (var property in type.GetPropertiesInHierarchy())
        {
            if (property.GetJsonPropertyName() is { } jsonName)
            {
                projection[new ProjectionMember().Append(new JsonMember(jsonName))] = new JsonScalarExpression(
                    element, [new PathSegment(jsonName)], System.Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType,
                    property.GetRelationalTypeMapping(), property.IsNullable);
            }
        }

        // Owned and complex members are JSON themselves, read as text.
        if (type is Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType)
        {
            foreach (var navigation in entityType.GetNavigationsInHierarchy()
                         .Where(n => n.ForeignKey.IsOwnership && n.TargetEntityType.IsMappedToJson() && (n.ForeignKey.PrincipalToDependent == n)))
            {
                var jsonName = navigation.TargetEntityType.GetJsonPropertyName()!;
                projection[new ProjectionMember().Append(new JsonMember(jsonName))] = new JsonScalarExpression(
                    element, [new PathSegment(jsonName)], typeof(string), stringMapping, !navigation.ForeignKey.IsRequiredDependent);
            }
        }

        foreach (var complexProperty in type.GetComplexProperties())
        {
            var jsonName = complexProperty.ComplexType.GetJsonPropertyName()!;
            projection[new ProjectionMember().Append(new JsonMember(jsonName))] = new JsonScalarExpression(
                element, [new PathSegment(jsonName)], typeof(string), stringMapping, jsonQueryExpression.IsNullable || complexProperty.IsNullable);
        }

        select.ReplaceProjection(projection);
        select.PushdownIntoSubquery();
        var subquery = select.Tables[0];
        var outer = CreateSelect(jsonQueryExpression, subquery, JsonArrayTableExpression.OrdinalColumn, typeof(int), intMapping);
        outer.AppendOrdering(new OrderingExpression(
            select.CreateColumnExpression(subquery, JsonArrayTableExpression.OrdinalColumn, typeof(int), intMapping, columnNullable: false), ascending: true));

        return new ShapedQueryExpression(
            outer,
            new RelationalStructuralTypeShaperExpression(
                type, new ProjectionBindingExpression(outer, new ProjectionMember(), typeof(ValueBuffer)), nullable: false));
    }
#pragma warning restore EF1001

    // A projection member named for a JSON property, which has no CLR member of its own.
    private sealed class JsonMember(string name) : System.Reflection.MemberInfo
    {
        public override Type? DeclaringType => throw new NotSupportedException();
        public override System.Reflection.MemberTypes MemberType => throw new NotSupportedException();
        public override string Name { get; } = name;
        public override Type? ReflectedType => throw new NotSupportedException();
        public override object[] GetCustomAttributes(bool inherit) => throw new NotSupportedException();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => throw new NotSupportedException();
        public override bool IsDefined(Type attributeType, bool inherit) => throw new NotSupportedException();
    }

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

    // JSON_SET is available to several providers, so partial ExecuteUpdate support belongs in the shared translator. The
    // dialect supplies the actual function and JSON conversion templates; a provider only needs to expose JsonSyntax.
#pragma warning disable EF1001 // EF's provider-facing JSON update extension point is internal by design.
    protected override SqlExpression? GenerateJsonPartialUpdateSetter(
        Expression target,
        SqlExpression value,
        ref SqlExpression? existingSetterValue)
    {
        var json = _dialect.Json ?? throw new UnreachableException();
        if (json.Set is null)
        {
            throw new NotSupportedException("This provider does not support JSON partial updates because the required mutation syntax is not ISO SQL.");
        }
        ColumnExpression column;
        IReadOnlyList<PathSegment> path;
        var useRawValue = false;

        if (target is JsonScalarExpression scalar)
        {
            if (scalar.Json is not ColumnExpression scalarColumn)
            {
                throw new UnreachableException();
            }

            column = scalarColumn;
            path = scalar.Path;
            useRawValue = scalar.TypeMapping?.ElementTypeMapping == null;
        }
        else if (target is JsonQueryExpression query)
        {
            column = query.JsonColumn;
            path = query.Path;
        }
        else
        {
            throw new UnreachableException();
        }

        var pathText = JsonPath(path);
        // Dialects with binary JSON can turn ordinary scalar SQL values into JSON, but temporal and binary
        // provider values have no jsonb conversion.  Cast those values to their SQL text form;
        // JSON stores them as strings, matching EF's JSON representation and converters.
        var valueType = value.TypeMapping?.ClrType ?? value.Type;
        var valueStoreType = value.TypeMapping?.StoreType ?? "";
        if (json.StoreType.Equals("jsonb", StringComparison.OrdinalIgnoreCase)
            && ((valueType == typeof(DateTime)) || (valueType == typeof(DateTimeOffset)) || (valueType == typeof(DateOnly))
                || (valueType == typeof(TimeOnly)) || (valueType == typeof(TimeSpan)) || (valueType == typeof(Guid))
                || (valueType == typeof(byte[])) || (valueType == typeof(char))
                || valueStoreType.Contains("DATE", StringComparison.OrdinalIgnoreCase)
                || valueStoreType.Contains("TIME", StringComparison.OrdinalIgnoreCase)
                || valueStoreType.Contains("INTERVAL", StringComparison.OrdinalIgnoreCase)
                || valueStoreType.Contains("UUID", StringComparison.OrdinalIgnoreCase)
                || valueStoreType.Contains("BYTEA", StringComparison.OrdinalIgnoreCase)))
        {
            value = _sqlExpressionFactory.Convert(value, typeof(string), _typeMappingSource.FindMapping(typeof(string)));
        }

        var replacement = useRawValue
            ? value
            : _sqlExpressionFactory.Call(json.FromText, [value], typeof(string), value.TypeMapping)!;
        var setter = _sqlExpressionFactory.Call(
            json.Set,
            [existingSetterValue ?? column, _sqlExpressionFactory.Constant(pathText), replacement],
            typeof(string),
            column.TypeMapping);

        if (existingSetterValue == null)
        {
            return setter;
        }

        existingSetterValue = setter;
        return null;
    }
#pragma warning restore EF1001

    private string JsonPath(IReadOnlyList<PathSegment> path)
        => JsonPathText.Of(path, _sqlGenerationHelper) is { Indexes.Count: 0 } text
            ? text.Literals[0]
            : throw new NotSupportedException("This provider's JSON partial updates require a constant JSON array index.");

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
    // Adapted from EF Core's SQLite provider (MIT, .NET Foundation):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqliteQueryableMethodTranslatingExpressionVisitor.cs
    // A JSON collection of structural types is that table, pushed down into a subquery ordered by the same ordinal.
    protected override bool IsNaturallyOrdered(SelectExpression selectExpression)
        => selectExpression is
            {
                Tables: [var table, ..],
                Orderings: [{ Expression: ColumnExpression { Name: JsonArrayTableExpression.OrdinalColumn } ordinal, IsAscending: true }]
            }
            && (ordinal.TableAlias == table.Alias)
            && (table is JsonArrayTableExpression || (table is SelectExpression { Tables: [JsonArrayTableExpression] }));

    private static Type Nullable(Type type)
        => type.IsValueType && (System.Nullable.GetUnderlyingType(type) == null) ? typeof(Nullable<>).MakeGenericType(type) : type;

    protected override bool IsValidSelectExpressionForExecuteDelete(SelectExpression selectExpression)
        => _dialect.Dml.CanDeleteDirectly(selectExpression) ?? base.IsValidSelectExpressionForExecuteDelete(selectExpression);
}

internal class AdoNetQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
    ISqlDialect dialect,
    ISqlGenerationHelper sqlGenerationHelper)
    : IQueryableMethodTranslatingExpressionVisitorFactory
{
    public virtual QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => new AdoNetQueryableMethodTranslatingExpressionVisitor(
            dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext, dialect, sqlGenerationHelper);
}

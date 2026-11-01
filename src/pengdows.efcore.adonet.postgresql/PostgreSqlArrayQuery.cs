using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     An array's elements as a table, in order: <c>unnest(array) WITH ORDINALITY AS t(value, ordinality)</c>. The column names are
///     given because an unnamed unnest column takes the table's alias, which EF may change. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Expressions/Internal/PgUnnestExpression.cs
/// </summary>
internal sealed class PostgreSqlUnnestExpression : TableValuedFunctionExpression
{
    public const string ValueColumn = "value";
    public const string OrdinalityColumn = "ordinality";

    public PostgreSqlUnnestExpression(string alias, SqlExpression array)
        : this(alias, array, annotations: null)
    {
    }

    private PostgreSqlUnnestExpression(
        string alias,
        SqlExpression array,
        IReadOnlyDictionary<string, Microsoft.EntityFrameworkCore.Infrastructure.IAnnotation>? annotations)
        : base(alias, "unnest", schema: null, builtIn: true, [array], annotations)
    {
    }

    public SqlExpression Array
        => Arguments[0];

    public PostgreSqlUnnestExpression Update(SqlExpression array)
        => array == Array ? this : new PostgreSqlUnnestExpression(Alias, array, Annotations);

    public override TableValuedFunctionExpression Update(IReadOnlyList<SqlExpression> arguments)
        => Update(arguments[0]);

    protected override Expression VisitChildren(ExpressionVisitor visitor)
        => Update((SqlExpression)visitor.Visit(Array));

    public override TableExpressionBase Clone(string? alias, ExpressionVisitor cloningExpressionVisitor)
        => new PostgreSqlUnnestExpression(alias ?? Alias, (SqlExpression)cloningExpressionVisitor.Visit(Array), Annotations);

    public override PostgreSqlUnnestExpression WithAlias(string newAlias)
        => new(newAlias, Array, Annotations);

    protected override PostgreSqlUnnestExpression WithAnnotations(
        IReadOnlyDictionary<string, Microsoft.EntityFrameworkCore.Infrastructure.IAnnotation> annotations)
        => new(Alias, Array, annotations);

    // Precompiled queries would quote this expression into code; the provider has none yet.
    public override Expression Quote()
        => throw new NotSupportedException("Queries over PostgreSQL arrays can't be precompiled.");

    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("unnest(");
        expressionPrinter.Visit(Array);
        expressionPrinter.Append(") WITH ORDINALITY AS ").Append(Alias);
    }

    public override bool Equals(object? obj)
        => (obj is PostgreSqlUnnestExpression) && base.Equals(obj);

    public override int GetHashCode()
        => base.GetHashCode();
}

/// <summary>The core's SQL, and <see cref="PostgreSqlUnnestExpression" />.</summary>
internal sealed class PostgreSqlQuerySqlGenerator(
    QuerySqlGeneratorDependencies dependencies,
    ISqlDialect dialect,
    ITypeMappingDialect typeMappings,
    bool reverseNullOrdering)
    : AdoNetQuerySqlGenerator(dependencies, dialect, typeMappings, reverseNullOrdering)
{
    protected override Expression VisitExtension(Expression extensionExpression)
    {
        if (extensionExpression is not PostgreSqlUnnestExpression unnest)
        {
            return base.VisitExtension(extensionExpression);
        }

        var helper = Dependencies.SqlGenerationHelper;
        Sql.Append("unnest(");
        Visit(unnest.Array);
        Sql.Append(") WITH ORDINALITY")
            .Append(AliasSeparator)
            .Append(helper.DelimitIdentifier(unnest.Alias))
            .Append("(")
            .Append(helper.DelimitIdentifier(PostgreSqlUnnestExpression.ValueColumn))
            .Append(", ")
            .Append(helper.DelimitIdentifier(PostgreSqlUnnestExpression.OrdinalityColumn))
            .Append(")");
        return unnest;
    }
}

internal sealed class PostgreSqlQuerySqlGeneratorFactory(
    QuerySqlGeneratorDependencies dependencies,
    ISqlDialect dialect,
    ITypeMappingDialect typeMappings,
    IAdoNetSingletonOptions options)
    : IQuerySqlGeneratorFactory
{
    public QuerySqlGenerator Create()
        => new PostgreSqlQuerySqlGenerator(dependencies, dialect, typeMappings, options.ReverseNullOrdering);
}

/// <summary>
///     The SQL of PostgreSQL's array operators, as templates (<see cref="SqlTemplate" />): an operand that is itself an
///     operation is parenthesized, since every operator here has the same precedence as <c>||</c>.
/// </summary>
internal static class PostgreSqlArraySql
{
    public const string AnyOperator = " = ANY ";

    public static SqlExpression Any(ISqlExpressionFactory factory, SqlExpression item, SqlExpression array, RelationalTypeMapping boolMapping)
        => factory.Call(Operand(item, 0) + AnyOperator + "(" + "{1})", [item, array], typeof(bool), boolMapping)!;

    public static SqlExpression Binary(
        ISqlExpressionFactory factory,
        string @operator,
        SqlExpression left,
        SqlExpression right,
        Type type,
        RelationalTypeMapping? typeMapping)
        => factory.Call(Operand(left, 0) + " " + @operator + " " + Operand(right, 1), [left, right], type, typeMapping)!;

    /// <summary>Whether <paramref name="expression" /> is <c>item = ANY (array)</c>, and its operands.</summary>
    public static bool IsAny(SqlExpression expression, [NotNullWhen(true)] out SqlExpression? item, [NotNullWhen(true)] out SqlExpression? array)
    {
        if ((expression is SqlFunctionExpression { Arguments: [var i, var a] } function)
            && SqlTemplate.TryGetTemplate(function, out var template)
            && template.Contains(AnyOperator, StringComparison.Ordinal))
        {
            item = i;
            array = a;
            return true;
        }

        item = null;
        array = null;
        return false;
    }

    private static string Operand(SqlExpression expression, int index)
        => expression is ColumnExpression or SqlConstantExpression or SqlParameterExpression
                or SqlUnaryExpression { OperatorType: ExpressionType.Convert }
            ||((expression is SqlFunctionExpression function) && !SqlTemplate.TryGetTemplate(function, out _))
            ? "{" + index + "}"
            : "({" + index + "})";
}

/// <summary>
///     A primitive collection mapped to a PostgreSQL array is queried through <c>unnest</c>, and the common operators over a whole
///     array are written with PostgreSQL's array operators instead of a subquery: Contains is <c>= ANY</c>, Count and Length
///     <c>cardinality</c>, an index a one-based subscript, Concat <c>||</c>, Any() a non-empty test, and Any or All of another
///     array's Contains overlap (<c>&amp;&amp;</c>) or containment (<c>&lt;@</c>). Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Internal/NpgsqlQueryableMethodTranslatingExpressionVisitor.cs
/// </summary>
internal sealed class PostgreSqlQueryableMethodTranslatingExpressionVisitor : AdoNetQueryableMethodTranslatingExpressionVisitor
{
    private readonly RelationalQueryCompilationContext _queryCompilationContext;
    private readonly ISqlExpressionFactory _sqlExpressionFactory;
    private readonly IRelationalTypeMappingSource _typeMappingSource;

    public PostgreSqlQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
        RelationalQueryCompilationContext queryCompilationContext,
        ISqlDialect dialect,
        ISqlGenerationHelper sqlGenerationHelper)
        : base(dependencies, relationalDependencies, queryCompilationContext, dialect, sqlGenerationHelper)
    {
        _queryCompilationContext = queryCompilationContext;
        _sqlExpressionFactory = relationalDependencies.SqlExpressionFactory;
        _typeMappingSource = relationalDependencies.TypeMappingSource;
    }

    private PostgreSqlQueryableMethodTranslatingExpressionVisitor(PostgreSqlQueryableMethodTranslatingExpressionVisitor parentVisitor)
        : base(parentVisitor)
    {
        _queryCompilationContext = parentVisitor._queryCompilationContext;
        _sqlExpressionFactory = parentVisitor._sqlExpressionFactory;
        _typeMappingSource = parentVisitor._typeMappingSource;
    }

    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new PostgreSqlQueryableMethodTranslatingExpressionVisitor(this);

    protected override ShapedQueryExpression? TranslatePrimitiveCollection(SqlExpression sqlExpression, IProperty? property, string tableAlias)
    {
        if (sqlExpression.TypeMapping is not PostgreSqlArrayTypeMapping arrayMapping)
        {
            return base.TranslatePrimitiveCollection(sqlExpression, property, tableAlias);
        }

        var elementClrType = JsonArrayTableExpression.ElementType(sqlExpression.Type);
        var isElementNullable = property?.GetElementType()?.IsNullable
            ?? (!elementClrType.IsValueType || (Nullable.GetUnderlyingType(elementClrType) != null));
        var (ordinality, ordinalityComparer) = Ordinality(tableAlias);

#pragma warning disable EF1001 // EF's provider-facing SelectExpression constructor, marked internal like the rest of its provider API.
        var select = new SelectExpression(
            [new PostgreSqlUnnestExpression(tableAlias, sqlExpression)],
            new ColumnExpression(
                PostgreSqlUnnestExpression.ValueColumn,
                tableAlias,
                Nullable.GetUnderlyingType(elementClrType) ?? elementClrType,
                arrayMapping.ElementTypeMapping,
                isElementNullable),
            identifier: [(ordinality, ordinalityComparer)],
            _queryCompilationContext.SqlAliasManager);
#pragma warning restore EF1001

        // unnest keeps the array's order, but a join loses it, so the order is said.
        select.AppendOrdering(new OrderingExpression(ordinality, ascending: true));
        return new ShapedQueryExpression(select, Shaper(select, elementClrType));
    }

    protected override ShapedQueryExpression? TranslateContains(ShapedQueryExpression source, Expression item)
    {
        if (!TryExtractArray(source, out var array, out var element, ignoreOrderings: true)
            || (TranslateExpression(item, applyDefaultTypeMapping: false) is not { } translatedItem))
        {
            return base.TranslateContains(source, item);
        }

        translatedItem = _sqlExpressionFactory.ApplyTypeMapping(translatedItem, element.TypeMapping)!;

        // null = ANY (...) is never true; array_position finds a null.
        if (translatedItem is SqlConstantExpression { Value: null })
        {
            return Simplified(
                source,
                _sqlExpressionFactory.IsNotNull(
                    _sqlExpressionFactory.Function(
                        "array_position", [array, translatedItem], nullable: true, [false, false], typeof(int), IntMapping)));
        }

        return Simplified(source, PostgreSqlArraySql.Any(_sqlExpressionFactory, translatedItem, array, BoolMapping));
    }

    protected override ShapedQueryExpression? TranslateCount(ShapedQueryExpression source, LambdaExpression? predicate)
    {
        if ((predicate != null) || !TryExtractArray(source, out var array, out _, ignoreOrderings: true))
        {
            return base.TranslateCount(source, predicate);
        }

#pragma warning disable EF1001 // EF's provider-facing SelectExpression constructor, marked internal like the rest of its provider API.
        return source.Update(
            new SelectExpression(Cardinality(array), _queryCompilationContext.SqlAliasManager),
            Expression.Convert(new ProjectionBindingExpression(source.QueryExpression, new ProjectionMember(), typeof(int?)), typeof(int)));
#pragma warning restore EF1001
    }

    protected override ShapedQueryExpression? TranslateAny(ShapedQueryExpression source, LambdaExpression? predicate)
    {
        if (!TryExtractArray(source, out var array, out var element, ignoreOrderings: true))
        {
            return base.TranslateAny(source, predicate);
        }

        if (predicate == null)
        {
            return Simplified(source, _sqlExpressionFactory.GreaterThan(Cardinality(array), _sqlExpressionFactory.Constant(0, IntMapping)));
        }

        // array.Any(e => other.Contains(e)) is array && other.
        if ((TranslateLambdaExpression(source, predicate) is { } translated)
            && PostgreSqlArraySql.IsAny(translated, out var item, out var other)
            && IsElement(item, element))
        {
            return Simplified(source, PostgreSqlArraySql.Binary(_sqlExpressionFactory, "&&", array, other, typeof(bool), BoolMapping));
        }

        return base.TranslateAny(source, predicate);
    }

    // array.All(e => other.Contains(e)) is array <@ other.
    protected override ShapedQueryExpression? TranslateAll(ShapedQueryExpression source, LambdaExpression predicate)
    {
        if (TryExtractArray(source, out var array, out var element, ignoreOrderings: true)
            && (TranslateLambdaExpression(source, predicate) is { } translated)
            && PostgreSqlArraySql.IsAny(translated, out var item, out var other)
            && IsElement(item, element))
        {
            return Simplified(source, PostgreSqlArraySql.Binary(_sqlExpressionFactory, "<@", array, other, typeof(bool), BoolMapping));
        }

        return base.TranslateAll(source, predicate);
    }

    // array[i] is a one-based subscript, which is null past the end as ElementAtOrDefault is; ElementAt is left to throw.
    protected override ShapedQueryExpression? TranslateElementAtOrDefault(ShapedQueryExpression source, Expression index, bool returnDefault)
    {
        if (returnDefault
            || !TryExtractArray(source, out var array, out var element)
            || (TranslateExpression(index) is not { } translatedIndex))
        {
            return base.TranslateElementAtOrDefault(source, index, returnDefault);
        }

        var oneBased = translatedIndex is SqlConstantExpression { Value: int constant }
            ? _sqlExpressionFactory.Constant(constant + 1, translatedIndex.TypeMapping)
            : _sqlExpressionFactory.Add(translatedIndex, _sqlExpressionFactory.Constant(1, translatedIndex.TypeMapping));
        var subscript = _sqlExpressionFactory.Call("{0}[{1}]", [array, oneBased], element.Type, element.TypeMapping)!;

#pragma warning disable EF1001 // EF's provider-facing SelectExpression constructor, marked internal like the rest of its provider API.
        return source.UpdateQueryExpression(new SelectExpression(subscript, _queryCompilationContext.SqlAliasManager));
#pragma warning restore EF1001
    }

    // a.Concat(b) is a || b, still an array.
    protected override ShapedQueryExpression TranslateConcat(ShapedQueryExpression source1, ShapedQueryExpression source2)
    {
        if (!TryExtractArray(source1, out var array1, out var element1)
            || !TryExtractArray(source2, out var array2, out var element2))
        {
            return base.TranslateConcat(source1, source2);
        }

        var tableAlias = ((SelectExpression)source1.QueryExpression).Tables[0].Alias!;
        var concatenation = PostgreSqlArraySql.Binary(
            _sqlExpressionFactory, "||", array1, array2, array1.Type, array1.TypeMapping ?? array2.TypeMapping);
        var (ordinality, ordinalityComparer) = Ordinality(tableAlias);

#pragma warning disable EF1001 // EF's provider-facing SelectExpression constructor, marked internal like the rest of its provider API.
        var select = new SelectExpression(
            [new PostgreSqlUnnestExpression(tableAlias, concatenation)],
            new ColumnExpression(
                PostgreSqlUnnestExpression.ValueColumn,
                tableAlias,
                element1.Type,
                element1.TypeMapping ?? element2.TypeMapping,
                element1.IsNullable || element2.IsNullable),
            identifier: [(ordinality, ordinalityComparer)],
            _queryCompilationContext.SqlAliasManager);
#pragma warning restore EF1001

        select.AppendOrdering(new OrderingExpression(ordinality, ascending: true));
        return new ShapedQueryExpression(select, Shaper(select, source1.ShaperExpression.Type));
    }

    /// <summary>
    ///     Whether <paramref name="source" /> is a whole array, unnested and unfiltered, in its own order unless
    ///     <paramref name="ignoreOrderings" />: then an operator over the array can stand for the query.
    /// </summary>
    private static bool TryExtractArray(
        ShapedQueryExpression source,
        [NotNullWhen(true)] out SqlExpression? array,
        [NotNullWhen(true)] out ColumnExpression? element,
        bool ignoreOrderings = false)
    {
        array = null;
        element = null;
        if ((source.QueryExpression is not SelectExpression
            {
                Tables: [PostgreSqlUnnestExpression unnest],
                Predicate: null,
                GroupBy: [],
                Having: null,
                IsDistinct: false,
                Limit: null,
                Offset: null
            } select)
            || (unnest.Array.TypeMapping is not PostgreSqlArrayTypeMapping))
        {
            return false;
        }

        var naturalOrder = (select.Orderings.Count == 0)
            || ((select.Orderings.Count == 1)
                && (select.Orderings[0].Expression is ColumnExpression { Name: PostgreSqlUnnestExpression.OrdinalityColumn } ordering)
                && (ordering.TableAlias == unnest.Alias));
        var shaper = source.ShaperExpression is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : source.ShaperExpression;
        if ((!ignoreOrderings && !naturalOrder)
            || (shaper is not ProjectionBindingExpression binding)
            || (select.GetProjection(binding) is not ColumnExpression { Name: PostgreSqlUnnestExpression.ValueColumn } column)
            || (column.TableAlias != unnest.Alias))
        {
            return false;
        }

        array = unnest.Array;
        element = column;
        return true;
    }

    private static bool IsElement(SqlExpression expression, ColumnExpression element)
        => expression is ColumnExpression column && (column.TableAlias == element.TableAlias) && (column.Name == element.Name);

    private SqlExpression Cardinality(SqlExpression array)
        => _sqlExpressionFactory.Function("cardinality", [array], nullable: true, [true], typeof(int), IntMapping);

    private (ColumnExpression Column, Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer Comparer) Ordinality(string tableAlias)
    {
        var mapping = _typeMappingSource.FindMapping(typeof(long))!;
        return (new ColumnExpression(PostgreSqlUnnestExpression.OrdinalityColumn, tableAlias, typeof(long), mapping, nullable: false), mapping.Comparer);
    }

    private static Expression Shaper(SelectExpression select, Type elementClrType)
    {
        var nullable = elementClrType.IsValueType && (Nullable.GetUnderlyingType(elementClrType) == null)
            ? typeof(Nullable<>).MakeGenericType(elementClrType)
            : elementClrType;
        Expression shaper = new ProjectionBindingExpression(select, new ProjectionMember(), nullable);
        return shaper.Type == elementClrType ? shaper : Expression.Convert(shaper, elementClrType);
    }

#pragma warning disable EF1001 // EF's provider-facing SelectExpression constructor, marked internal like the rest of its provider API.
    private ShapedQueryExpression Simplified(ShapedQueryExpression source, SqlExpression translation)
        => source.Update(
            new SelectExpression(translation, _queryCompilationContext.SqlAliasManager),
            Expression.Convert(new ProjectionBindingExpression(translation, new ProjectionMember(), typeof(bool?)), typeof(bool)));
#pragma warning restore EF1001

    private RelationalTypeMapping BoolMapping
        => _typeMappingSource.FindMapping(typeof(bool))!;

    private RelationalTypeMapping IntMapping
        => _typeMappingSource.FindMapping(typeof(int))!;
}

internal sealed class PostgreSqlQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
    ISqlDialect dialect,
    ISqlGenerationHelper sqlGenerationHelper)
    : IQueryableMethodTranslatingExpressionVisitorFactory
{
    public QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => new PostgreSqlQueryableMethodTranslatingExpressionVisitor(
            dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext, dialect, sqlGenerationHelper);
}

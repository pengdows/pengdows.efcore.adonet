// Behaviour reference (not copied): EF Core's SQLite JSON array table implementation (MIT, .NET Foundation):
// https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqlExpressions/JsonEachExpression.cs

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     The elements of a JSON array as a table: an ordinal column (<see cref="OrdinalColumn" />) that orders them and a value
///     column (<see cref="ValueColumn" />) of the element's store type, written through the dialect's
///     <see cref="JsonSyntax.ArrayElements" />. A primitive collection (a collection column, or a parameter collection that
///     isn't expanded into an IN list) is queried through it. A parameter's element type is known only once the query has
///     inferred it, so <see cref="ElementTypeMapping" /> starts null and is supplied by the type-mapping pass. Behaviour
///     reference (not copied): EF Core's SQLite JSON array table implementation (MIT, .NET Foundation):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqlExpressions/JsonEachExpression.cs
/// </summary>
internal sealed class JsonArrayTableExpression : TableValuedFunctionExpression
{
    public const string OrdinalColumn = "key";
    public const string ValueColumn = "value";

    public JsonArrayTableExpression(string alias, SqlExpression json, RelationalTypeMapping? elementTypeMapping)
        : this(alias, json, elementTypeMapping, annotations: null)
    {
    }

    private JsonArrayTableExpression(
        string alias,
        SqlExpression json,
        RelationalTypeMapping? elementTypeMapping,
        IReadOnlyDictionary<string, Microsoft.EntityFrameworkCore.Infrastructure.IAnnotation>? annotations)
        : base(alias, "json array elements", schema: null, builtIn: true, [json], annotations)
        => ElementTypeMapping = elementTypeMapping;

    public SqlExpression Json
        => Arguments[0];

    /// <summary>The element type of a collection type.</summary>
    public static Type ElementType(Type collection)
    {
        if (collection.IsArray)
        {
            return collection.GetElementType()!;
        }

        var enumerable = collection.GetInterfaces()
            .Append(collection)
            .FirstOrDefault(interfaceType => interfaceType.IsGenericType
                && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0]
            ?? throw new ArgumentException($"'{collection}' is not a generic enumerable.", nameof(collection));
    }

    public RelationalTypeMapping? ElementTypeMapping { get; }

    public JsonArrayTableExpression Update(SqlExpression json, RelationalTypeMapping? elementTypeMapping)
    {
        if ((json == Json) && (elementTypeMapping == ElementTypeMapping))
        {
            return this;
        }

        return new JsonArrayTableExpression(Alias, json, elementTypeMapping, Annotations);
    }

    public override TableValuedFunctionExpression Update(IReadOnlyList<SqlExpression> arguments)
        => Update(arguments[0], ElementTypeMapping);

    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var visitedJson = (SqlExpression)visitor.Visit(Json);
        return Update(visitedJson, ElementTypeMapping);
    }

    public override TableExpressionBase Clone(string? alias, ExpressionVisitor cloningExpressionVisitor)
        => new JsonArrayTableExpression(
            alias ?? Alias, (SqlExpression)cloningExpressionVisitor.Visit(Json), ElementTypeMapping, Annotations);

    public override JsonArrayTableExpression WithAlias(string newAlias)
        => new JsonArrayTableExpression(newAlias, Json, ElementTypeMapping, Annotations);

    protected override JsonArrayTableExpression WithAnnotations(
        IReadOnlyDictionary<string, Microsoft.EntityFrameworkCore.Infrastructure.IAnnotation> annotations)
        => new JsonArrayTableExpression(Alias, Json, ElementTypeMapping, annotations);

    // Precompiled queries would quote this expression into code; the provider has none yet.
    public override Expression Quote()
        => throw new NotSupportedException("Queries over primitive collections can't be precompiled.");

    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("JSON ARRAY ELEMENTS(");
        expressionPrinter.Visit(Json);
        expressionPrinter.Append(") AS ").Append(Alias);
    }

    public override bool Equals(object? obj)
        => obj is JsonArrayTableExpression other && base.Equals(other) && Equals(ElementTypeMapping, other.ElementTypeMapping);

    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), ElementTypeMapping);
}

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     How the MySQL JSON packages (System.Text.Json, Newtonsoft.Json) track changes inside a property mapped to a json column,
///     as Pomelo's options of the same name. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Infrastructure/MySqlCommonJsonChangeTrackingOptions.cs
/// </summary>
public enum MySqlCommonJsonChangeTrackingOptions
{
    /// <summary>Only a new value of the property is a change; a change inside it is not. The fastest.</summary>
    RootPropertyOnly,

    /// <summary>A change anywhere inside is a change, found by comparing the serialized JSON text.</summary>
    FullHierarchyOptimizedFast,

    /// <summary>A change anywhere inside is a change, found by comparing the JSON documents (whitespace and key order aside).</summary>
    FullHierarchyOptimizedSemantically,

    /// <summary>As <see cref="FullHierarchyOptimizedSemantically" />: the documents are compared, never their spelling.</summary>
    FullHierarchySemantically
}

/// <summary>A json column's value as its JSON text, which a converter turns into the property's type; literals in the dialect's quoting.</summary>
internal sealed class MySqlJsonTextMapping : StringTypeMapping
{
    private readonly ISqlDialect _sql;

    public MySqlJsonTextMapping(string storeType, ISqlDialect sql)
        : base(storeType, System.Data.DbType.String, unicode: true)
        => _sql = sql;

    private MySqlJsonTextMapping(RelationalTypeMappingParameters parameters, ISqlDialect sql)
        : base(parameters)
        => _sql = sql;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new MySqlJsonTextMapping(parameters, _sql);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _sql.StringLiteral((string)value);
}

/// <summary>
///     The mappings a JSON package adds: its document types (JsonDocument, JToken ...) on a json column whatever the column says,
///     and any other object (a POCO, or a collection of them) on a column the model names json. A string, a scalar and a
///     collection of scalars stay the core's (a primitive collection). Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlJsonTypeMappingSourcePlugin.cs
/// </summary>
internal abstract class MySqlJsonMappingSourcePlugin(ISqlDialect sql) : IRelationalTypeMappingSourcePlugin
{
    protected abstract bool IsDocument(Type type);

    protected abstract ValueConverter Converter(Type type);

    protected abstract ValueComparer Comparer(Type type);

    public RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        if ((sql.Json is not { } json) || (mappingInfo.ClrType is not { } type))
        {
            return null;
        }

        var storeType = mappingInfo.StoreTypeName;
        var onJsonColumn = (storeType != null) && string.Equals(storeType, json.StoreType, StringComparison.OrdinalIgnoreCase);
        if ((storeType != null) && !onJsonColumn)
        {
            return null;
        }

        if (!IsDocument(type) && !(onJsonColumn && IsObject(type)))
        {
            return null;
        }

        return (RelationalTypeMapping)new MySqlJsonTextMapping(storeType ?? json.StoreType, sql).WithComposedConverter(Converter(type), Comparer(type));
    }

    // Not a string, a scalar, or a collection of scalars.
    private static bool IsObject(Type type)
    {
        if (IsScalar(type))
        {
            return false;
        }

        var element = type.IsArray
            ? type.GetElementType()
            : type.GetInterfaces().Append(type)
                .FirstOrDefault(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))?.GetGenericArguments()[0];
        return (element == null) || !IsScalar(element);
    }

    private static bool IsScalar(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive || t.IsEnum || (t == typeof(string)) || (t == typeof(decimal)) || (t == typeof(DateTime))
               || (t == typeof(DateTimeOffset)) || (t == typeof(DateOnly)) || (t == typeof(TimeOnly)) || (t == typeof(TimeSpan))
               || (t == typeof(Guid)) || (t == typeof(byte[]));
    }
}

/// <summary>
///     Reads inside a json column in queries, as EF's own JSON columns are read (a <see cref="JsonScalarExpression" />, which the
///     dialect writes with JSON_EXTRACT): a POCO's member (by the JSON package's property name), a list's element by index, a
///     collection's Count, Length and Any() as JSON_LENGTH. A JSON package adds its document types' members on top. Behaviour
///     reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionTranslators/Internal/MySqlJsonPocoTranslator.cs
/// </summary>
internal class MySqlJsonPathTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    Func<MemberInfo, string?> jsonPropertyName) : IMemberTranslator, IMethodCallTranslator
{
    private static readonly MethodInfo AnyWithoutPredicate = typeof(Enumerable).GetMethods()
        .Single(m => (m.Name == nameof(Enumerable.Any)) && (m.GetParameters().Length == 1));

    private static readonly MethodInfo CountWithoutPredicate = typeof(Enumerable).GetMethods()
        .Single(m => (m.Name == nameof(Enumerable.Count)) && (m.GetParameters().Length == 1));

    // EF reads a list's indexer as ElementAt(source, index).
    private static readonly MethodInfo ElementAt = typeof(Enumerable).GetMethods()
        .Single(m => (m.Name == nameof(Enumerable.ElementAt)) && (m.GetParameters()[1].ParameterType == typeof(int)));

    protected ISqlExpressionFactory SqlExpressionFactory
        => sqlExpressionFactory;

    public virtual SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((instance == null) || !IsJson(instance))
        {
            return null;
        }

        if ((member.Name is "Count" or "Length") && (returnType == typeof(int)) && IsCollection(instance.Type))
        {
            return Length(instance);
        }

        return member is PropertyInfo or FieldInfo
            ? Append(instance, new PathSegment(jsonPropertyName(member) ?? member.Name), returnType)
            : null;
    }

    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((instance != null) && IsJson(instance) && IsCollection(instance.Type) && (method.Name == "get_Item")
            && (arguments is [{ Type: var indexType } index]) && (indexType == typeof(int)))
        {
            return Append(instance, new PathSegment(sqlExpressionFactory.ApplyDefaultTypeMapping(index)), method.ReturnType);
        }

        if ((instance == null) && method.IsGenericMethod && (method.GetGenericMethodDefinition() == ElementAt)
            && (arguments is [var list, var position]) && IsJson(list))
        {
            return Append(list, new PathSegment(sqlExpressionFactory.ApplyDefaultTypeMapping(position)), method.ReturnType);
        }

        if ((instance == null) && method.IsGenericMethod && (arguments is [var source]) && IsJson(source))
        {
            var definition = method.GetGenericMethodDefinition();
            if (definition == AnyWithoutPredicate)
            {
                return sqlExpressionFactory.GreaterThan(Length(source), sqlExpressionFactory.Constant(0));
            }

            if (definition == CountWithoutPredicate)
            {
                return Length(source);
            }
        }

        return null;
    }

    // A json column, or a value read inside one that isn't a scalar.
    protected static bool IsJson(SqlExpression expression)
        => (expression is ColumnExpression or JsonScalarExpression)
           && ((expression.TypeMapping is MySqlJsonTextMapping) || (expression is JsonScalarExpression { TypeMapping.ElementTypeMapping: not null }));

    protected static bool IsCollection(Type type)
        => (type != typeof(string)) && typeof(IEnumerable).IsAssignableFrom(type);

    // The value at one more step of the path: a scalar read as its type, anything else as its JSON.
    protected SqlExpression Append(SqlExpression json, PathSegment segment, Type returnType)
    {
        var (column, path) = json is JsonScalarExpression scalar ? (scalar.Json, scalar.Path.Append(segment).ToArray()) : (json, new[] { segment });
        return new JsonScalarExpression(column, path, returnType, Mapping(returnType), nullable: true);
    }

    // The same value read as another type (JsonElement.GetInt32, JToken.Value<T>).
    protected SqlExpression As(SqlExpression json, Type returnType)
        => json is JsonScalarExpression scalar
            ? new JsonScalarExpression(scalar.Json, scalar.Path, returnType, Mapping(returnType), nullable: true)
            : new JsonScalarExpression(json, [], returnType, Mapping(returnType), nullable: true);

    protected SqlExpression Length(SqlExpression json)
        => sqlExpressionFactory.Call("JSON_LENGTH({0})", [json], typeof(int), typeMappingSource.FindMapping(typeof(int)))!;

    private RelationalTypeMapping? Mapping(Type type)
        => typeMappingSource.FindMapping(type) ?? typeMappingSource.FindMapping(type, "json");
}

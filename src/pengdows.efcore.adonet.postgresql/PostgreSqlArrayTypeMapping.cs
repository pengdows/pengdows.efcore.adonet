using System.Collections;
using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     PostgreSQL arrays for primitive collections. Npgsql's provider maps every <c>T[]</c> and <c>List&lt;T&gt;</c> to an array by
///     default; here a primitive collection is a jsonb document, as on every other provider of this library, so a model moves
///     between databases unchanged and the columns earlier versions created keep their type. A model takes arrays with
///     <see cref="UsePostgreSqlArrays" />, and a property takes one by its column type (<c>HasColumnType("text[]")</c>), which is
///     how an application moving from Npgsql keeps its schema. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/NpgsqlTypeMappingSource.cs
/// </summary>
public static class PostgreSqlArrayExtensions
{
    internal const string ArraysAnnotation = "PostgreSql:Arrays";

    /// <summary>
    ///     Maps every primitive collection (<c>T[]</c> or <c>List&lt;T&gt;</c> of a scalar type) without a column type of its own
    ///     to a PostgreSQL array of its element's type, as Npgsql's provider does, instead of a jsonb document. Collections inside
    ///     a JSON document stay JSON.
    /// </summary>
    public static ModelBuilder UsePostgreSqlArrays(this ModelBuilder modelBuilder)
        => modelBuilder.HasAnnotation(ArraysAnnotation, true);
}

/// <summary>
///     A collection bound, read and written as a PostgreSQL array. Npgsql binds and reads <c>T[]</c> and <c>List&lt;T&gt;</c>
///     itself; the parameter is typed as an array of the element's type, so an empty or null array still has one. Behaviour
///     reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/Mapping/NpgsqlArrayTypeMapping.cs
/// </summary>
internal sealed class PostgreSqlArrayTypeMapping : RelationalTypeMapping
{
    private PostgreSqlArrayTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    public override RelationalTypeMapping ElementTypeMapping
        => (RelationalTypeMapping)base.ElementTypeMapping!;

    /// <summary>The mapping of <paramref name="collectionType" /> (a one-dimensional array or a List) to <paramref name="storeType" />.</summary>
    public static PostgreSqlArrayTypeMapping? Create(Type collectionType, string storeType, RelationalTypeMapping elementMapping)
    {
        var elementType = ElementType(collectionType);
        if (elementType == null)
        {
            return null;
        }

        var comparer = (ValueComparer)Activator.CreateInstance(ComparerType(collectionType, elementType), ElementComparer(elementType, elementMapping))!;
        return new PostgreSqlArrayTypeMapping(
            new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(collectionType, converter: null, comparer, elementMapping: elementMapping),
                storeType));
    }

    /// <summary>The element type of a one-dimensional array or a List, other than byte[] (bytea); null for anything else.</summary>
    public static Type? ElementType(Type collectionType)
    {
        if (collectionType == typeof(byte[]))
        {
            return null;
        }

        if (collectionType.IsArray)
        {
            return collectionType.GetArrayRank() == 1 ? collectionType.GetElementType() : null;
        }

        return collectionType.IsGenericType && (collectionType.GetGenericTypeDefinition() == typeof(List<>))
            ? collectionType.GetGenericArguments()[0]
            : null;
    }

    private static Type ComparerType(Type collectionType, Type elementType)
    {
        if (Nullable.GetUnderlyingType(elementType) is { } underlying)
        {
            return typeof(ListOfNullableValueTypesComparer<,>).MakeGenericType(collectionType, underlying);
        }

        return elementType.IsValueType
            ? typeof(ListOfValueTypesComparer<,>).MakeGenericType(collectionType, elementType)
            : typeof(ListOfReferenceTypesComparer<,>).MakeGenericType(collectionType, elementType);
    }

    private static ValueComparer ElementComparer(Type elementType, RelationalTypeMapping elementMapping)
        => elementMapping.Comparer.Type == elementType ? elementMapping.Comparer : ValueComparer.CreateDefault(elementType, favorStructuralComparisons: false);

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlArrayTypeMapping(parameters);

    // Npgsql binds arrays and Lists; a parameter collection composed on the client (ids.Skip(1)) is some other enumerable.
    public override DbParameter CreateParameter(
        DbCommand command,
        string name,
        object? value,
        bool? nullable = null,
        ParameterDirection direction = ParameterDirection.Input)
    {
        if ((value is IEnumerable elements) && (ElementType(value.GetType()) == null))
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ElementType(ClrType)!))!;
            foreach (var element in elements)
            {
                list.Add(element);
            }

            value = list;
        }

        return base.CreateParameter(command, name, value, nullable, direction);
    }

    // An array of the element's type, which Npgsql takes from the element's DbType, so null and empty arrays are typed too.
    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if ((parameter is NpgsqlParameter npgsql) && (ElementTypeMapping.DbType is { } elementDbType))
        {
            npgsql.NpgsqlDbType = NpgsqlDbType.Array | new NpgsqlParameter { DbType = elementDbType }.NpgsqlDbType;
        }
    }

    // ARRAY[e1,e2]::type[]; the cast types an empty array.
    protected override string GenerateNonNullSqlLiteral(object value)
    {
        var sql = new StringBuilder("ARRAY[");
        var first = true;
        foreach (var element in (IEnumerable)value)
        {
            if (!first)
            {
                sql.Append(',');
            }

            first = false;
            sql.Append(ElementTypeMapping.GenerateProviderValueSqlLiteral(element));
        }

        return sql.Append("]::").Append(StoreType).ToString();
    }
}

/// <summary>The core's type mappings, and an array of any of them for a store type ending in <c>[]</c>.</summary>
internal sealed class PostgreSqlTypeMappingSource(
    TypeMappingSourceDependencies dependencies,
    RelationalTypeMappingSourceDependencies relationalDependencies,
    ITypeMappingDialect typeMappings,
    ISqlDialect sql)
    : AdoNetTypeMappingSource(dependencies, relationalDependencies, typeMappings, sql)
{
    // PostgreSQL's own types, each read and bound as Npgsql's CLR type for it; the first of a CLR type is its default.
    private static readonly PostgreSqlProviderTypeMapping[] ProviderTypes =
    [
        new(typeof(NpgsqlTypes.NpgsqlTsVector), "tsvector", NpgsqlTypes.NpgsqlDbType.TsVector),
        new(typeof(NpgsqlTypes.NpgsqlTsQuery), "tsquery", NpgsqlTypes.NpgsqlDbType.TsQuery),
        new(typeof(System.Net.IPAddress), "inet", NpgsqlTypes.NpgsqlDbType.Inet),
        new(typeof(NpgsqlTypes.NpgsqlInet), "inet", NpgsqlTypes.NpgsqlDbType.Inet),
        new(typeof(System.Net.IPNetwork), "cidr", NpgsqlTypes.NpgsqlDbType.Cidr),
        new(typeof(NpgsqlTypes.NpgsqlCidr), "cidr", NpgsqlTypes.NpgsqlDbType.Cidr),
        new(typeof(System.Net.NetworkInformation.PhysicalAddress), "macaddr", NpgsqlTypes.NpgsqlDbType.MacAddr),
        new(typeof(System.Net.NetworkInformation.PhysicalAddress), "macaddr8", NpgsqlTypes.NpgsqlDbType.MacAddr8),
        .. PostgreSqlRangeTypes.Mappings
    ];

    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
        => FindArrayMapping(mappingInfo) ?? FindProviderTypeMapping(mappingInfo) ?? base.FindMapping(mappingInfo);

    private static RelationalTypeMapping? FindProviderTypeMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        var storeType = mappingInfo.StoreTypeNameBase ?? mappingInfo.StoreTypeName;
        foreach (var mapping in ProviderTypes)
        {
            var clrMatches = (mappingInfo.ClrType == null) || (mappingInfo.ClrType == mapping.ClrType);
            var storeMatches = (storeType == null) || string.Equals(storeType, mapping.StoreType, StringComparison.OrdinalIgnoreCase);
            if (clrMatches && storeMatches && ((mappingInfo.ClrType != null) || (storeType != null)))
            {
                return mapping;
            }
        }

        return null;
    }

    private RelationalTypeMapping? FindArrayMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        if ((mappingInfo.StoreTypeName?.Trim() is not { } storeType) || !storeType.EndsWith("[]", StringComparison.Ordinal))
        {
            return null;
        }

        var elementStoreType = storeType[..^2].TrimEnd();
        var collectionType = mappingInfo.ClrType;
        var elementType = collectionType == null ? null : PostgreSqlArrayTypeMapping.ElementType(collectionType);
        if ((collectionType != null) && (elementType == null))
        {
            return null;
        }

        var elementMapping = elementType == null
            ? FindMapping(elementStoreType)
            : FindMapping(Nullable.GetUnderlyingType(elementType) ?? elementType, elementStoreType);
        if ((elementMapping == null) || (elementMapping.ElementTypeMapping != null))
        {
            return null;
        }

        return PostgreSqlArrayTypeMapping.Create(collectionType ?? elementMapping.ClrType.MakeArrayType(), storeType, elementMapping);
    }
}

/// <summary>
///     With <see cref="PostgreSqlArrayExtensions.UsePostgreSqlArrays" />, gives each primitive collection without a column type
///     the array type of its element. Runs before EF assigns the type mappings the column type decides.
/// </summary>
internal sealed class PostgreSqlArrayConvention(IRelationalTypeMappingSource typeMappingSource) : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        if (modelBuilder.Metadata.FindAnnotation(PostgreSqlArrayExtensions.ArraysAnnotation)?.Value is not true)
        {
            return;
        }

        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes().Where(e => !e.IsMappedToJson()))
        {
            foreach (var property in entityType.GetDeclaredProperties())
            {
                if (ArrayStoreType(property) is { } storeType)
                {
                    property.Builder.HasColumnType(storeType);
                }
            }
        }
    }

    private string? ArrayStoreType(IConventionProperty property)
    {
        if (!property.IsPrimitiveCollection
            || (property.GetColumnType() != null)
            || (property.GetValueConverter() != null)
            || (PostgreSqlArrayTypeMapping.ElementType(property.ClrType) is not { } elementType))
        {
            return null;
        }

        var elementMapping = typeMappingSource.FindMapping(Nullable.GetUnderlyingType(elementType) ?? elementType);
        return (elementMapping == null) || (elementMapping.ElementTypeMapping != null) ? null : elementMapping.StoreType + "[]";
    }
}

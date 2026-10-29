using System.Net;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.postgresql;

internal sealed class PostgreSqlProviderTypeMappingPlugin(IServiceProvider services) : IRelationalTypeMappingSourcePlugin
{
    public RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        if (mappingInfo.ClrType == typeof(IPAddress)
            && (mappingInfo.StoreTypeName == null || IsStoreType(mappingInfo, "inet")))
        {
            return new PostgreSqlProviderTypeMapping(typeof(IPAddress), "inet");
        }

        if (mappingInfo.ClrType is { IsGenericType: true } range
            && range.GetGenericTypeDefinition() == typeof(NpgsqlRange<>)
            && mappingInfo.StoreTypeNameBase is { } rangeStore
            && rangeStore.EndsWith("range", StringComparison.OrdinalIgnoreCase))
        {
            return new PostgreSqlProviderTypeMapping(range, mappingInfo.StoreTypeName!);
        }

        if (mappingInfo.ClrType is { IsArray: true } arrayType
            && arrayType != typeof(byte[])
            && mappingInfo.StoreTypeName is { } arrayStore
            && arrayStore.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementType = arrayType.GetElementType()!;
            var element = services.GetRequiredService<IRelationalTypeMappingSource>().FindMapping(elementType, arrayStore[..^2]);
            return element == null ? null : new PostgreSqlArrayTypeMapping(arrayType, arrayStore, element);
        }

        return null;
    }

    private static bool IsStoreType(in RelationalTypeMappingInfo info, string type)
        => string.Equals(info.StoreTypeNameBase ?? info.StoreTypeName, type, StringComparison.OrdinalIgnoreCase);
}

internal sealed class PostgreSqlProviderTypeMapping : RelationalTypeMapping
{
    public PostgreSqlProviderTypeMapping(Type clrType, string storeType)
        : this(new RelationalTypeMappingParameters(new CoreTypeMappingParameters(clrType), storeType))
    {
    }

    private PostgreSqlProviderTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlProviderTypeMapping(parameters);

    protected override string GenerateNonNullSqlLiteral(object value)
        => throw new NotSupportedException("PostgreSQL provider-specific literals require their native type syntax.");
}

internal sealed class PostgreSqlArrayTypeMapping : RelationalTypeMapping, IArrayTypeMapping
{
    public PostgreSqlArrayTypeMapping(Type clrType, string storeType, RelationalTypeMapping element)
        : this(new RelationalTypeMappingParameters(
            new CoreTypeMappingParameters(clrType, elementMapping: element), storeType))
    {
    }

    private PostgreSqlArrayTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    public string Elements => "(SELECT ROW_NUMBER() OVER () - 1 AS {1}, unnest({0}) AS {2})";
    public string? Contains => "{1} = ANY({0})";
    public string? Count => "CARDINALITY({0})";
    public string? ElementAt => "({0})[{1} + 1]";

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlArrayTypeMapping(parameters);

    protected override string GenerateNonNullSqlLiteral(object value)
        => "ARRAY[" + string.Join(", ", ((Array)value).Cast<object>().Select(v => ((RelationalTypeMapping)ElementTypeMapping!).GenerateSqlLiteral(v))) + "]";
}

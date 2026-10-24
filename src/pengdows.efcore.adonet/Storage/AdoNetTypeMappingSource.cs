using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Maps CLR types to EF's stock relational mappings, which bind parameters via the portable
///     <see cref="DbType" /> (never provider-specific parameter types). Store type names come from the dialect.
/// </summary>
public class AdoNetTypeMappingSource : RelationalTypeMappingSource
{
    private readonly Dictionary<Type, RelationalTypeMapping> _clrTypeMappings = [];
    private readonly Dictionary<string, RelationalTypeMapping> _storeTypeMappings = new(StringComparer.OrdinalIgnoreCase);

    public AdoNetTypeMappingSource(
        TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies,
        AdoNetSqlDialect dialect)
        : base(dependencies, relationalDependencies)
    {
        foreach (var clrType in AdoNetSqlDialect.SupportedClrTypes)
        {
            if (dialect.GetStoreType(clrType) is not { } storeType)
            {
                continue;
            }

            var mapping = CreateMapping(clrType, storeType, dialect);
            _clrTypeMappings[clrType] = mapping;
            _storeTypeMappings.TryAdd(storeType, mapping);
        }
    }

    protected virtual RelationalTypeMapping CreateMapping(Type clrType, string storeType, AdoNetSqlDialect dialect)
        => clrType switch
        {
            _ when clrType == typeof(bool) => new AdoNetBoolTypeMapping(storeType, dialect.TrueLiteral, dialect.FalseLiteral),
            _ when clrType == typeof(byte) => new ByteTypeMapping(storeType),
            _ when clrType == typeof(short) => new ShortTypeMapping(storeType),
            _ when clrType == typeof(int) => new IntTypeMapping(storeType),
            _ when clrType == typeof(long) => new LongTypeMapping(storeType),
            _ when clrType == typeof(float) => new FloatTypeMapping(storeType),
            _ when clrType == typeof(double) => new DoubleTypeMapping(storeType),
            _ when clrType == typeof(decimal) => new DecimalTypeMapping(storeType),
            _ when clrType == typeof(string) => new StringTypeMapping(storeType, DbType.String, unicode: true),
            _ when clrType == typeof(DateTime) => new DateTimeTypeMapping(storeType),
            _ when clrType == typeof(DateTimeOffset) => new DateTimeOffsetTypeMapping(storeType),
            _ when clrType == typeof(DateOnly) => new DateOnlyTypeMapping(storeType),
            _ when clrType == typeof(TimeOnly) => new TimeOnlyTypeMapping(storeType),
            _ when clrType == typeof(TimeSpan) => new TimeSpanTypeMapping(storeType),
            _ when clrType == typeof(Guid) => new GuidTypeMapping(storeType),
            _ when clrType == typeof(byte[]) => new ByteArrayTypeMapping(storeType),
            _ => throw new NotSupportedException($"No mapping for CLR type '{clrType}'.")
        };

    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        // Plugins registered through IRelationalTypeMappingSourcePlugin win.
        var mapping = base.FindMapping(mappingInfo);
        if (mapping != null)
        {
            return mapping;
        }

        if (mappingInfo.ClrType is { } clrType
            && _clrTypeMappings.TryGetValue(clrType, out mapping))
        {
            return mappingInfo.StoreTypeName is { } storeTypeName
                ? mapping.WithStoreTypeAndSize(storeTypeName, mappingInfo.Size)
                : mapping;
        }

        if (mappingInfo.StoreTypeName is { } storeType
            && (_storeTypeMappings.TryGetValue(storeType, out mapping)
                || (mappingInfo.StoreTypeNameBase is { } storeTypeBase
                    && _storeTypeMappings.TryGetValue(storeTypeBase, out mapping))))
        {
            return mapping.WithStoreTypeAndSize(storeType, mappingInfo.Size);
        }

        return null;
    }
}

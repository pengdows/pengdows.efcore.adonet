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
    private readonly ITypeMappingDialect _typeMappings;

    public AdoNetTypeMappingSource(
        TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies,
        ITypeMappingDialect typeMappings,
        ISqlDialect sql)
        : base(dependencies, relationalDependencies)
    {
        _typeMappings = typeMappings;

        foreach (var clrType in AnsiTypeMappings.SupportedClrTypes)
        {
            if (typeMappings.GetStoreType(clrType) is not { } storeType)
            {
                continue;
            }

            var mapping = CreateMapping(clrType, storeType, sql);
            _clrTypeMappings[clrType] = mapping;
            _storeTypeMappings.TryAdd(storeType, mapping);

            // Also by base name, so DECIMAL(10,4) finds the DECIMAL(18,2) mapping.
            if (storeType.IndexOf('(') is > 0 and var parenthesis)
            {
                _storeTypeMappings.TryAdd(storeType[..parenthesis].TrimEnd(), mapping);
            }
        }
    }

    protected virtual RelationalTypeMapping CreateMapping(Type clrType, string storeType, ISqlDialect sql)
        => clrType switch
        {
            _ when clrType == typeof(bool) => new AdoNetBoolTypeMapping(storeType, sql.TrueLiteral, sql.FalseLiteral),
            _ when clrType == typeof(byte) => new AdoNetByteTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(short) => new AdoNetShortTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(int) => new AdoNetIntTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(long) => new AdoNetLongTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(float) => new AdoNetFloatTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(double) => new AdoNetDoubleTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(decimal) => DecimalMapping(storeType),
            _ when clrType == typeof(string) => new AdoNetStringTypeMapping(storeType, sql, _typeMappings),
            // DateTime2 means "no offset" everywhere; Npgsql reads DbType.DateTime as timestamptz.
            _ when clrType == typeof(DateTime) => new AdoNetDateTimeTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(DateTimeOffset) => new AdoNetDateTimeOffsetTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(DateOnly) => new AdoNetDateOnlyTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(TimeOnly) => new AdoNetTimeOnlyTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(TimeSpan) => new AdoNetTimeSpanTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(Guid) => new AdoNetGuidTypeMapping(storeType, _typeMappings),
            _ when clrType == typeof(byte[]) => new AdoNetByteArrayTypeMapping(storeType, _typeMappings),
            _ => throw new NotSupportedException($"No mapping for CLR type '{clrType}'.")
        };

    // The dialect's default store type, e.g. DECIMAL(18,2), carries the precision and scale parameters bind with.
    private RelationalTypeMapping DecimalMapping(string storeType)
    {
        var (precision, scale) = DecimalFacets(storeType);
        return new AdoNetDecimalTypeMapping(storeType, _typeMappings, precision, scale);
    }

    // DECIMAL(p,s), DECIMAL(p) meaning scale 0, or plain DECIMAL leaving both to the database.
    private static (int? Precision, int? Scale) DecimalFacets(string storeType)
    {
        var facets = (storeType.IndexOf('(') is > 0 and var open) && storeType.EndsWith(')')
            ? storeType[(open + 1)..^1].Split(',')
            : [];
        int? precision = (facets.Length > 0) && int.TryParse(facets[0], out var p) ? p : null;
        int? scale = (facets.Length > 1) && int.TryParse(facets[1], out var s) ? s : null;
        return (precision, precision == null ? null : (scale ?? 0));
    }

    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        // Plugins registered through IRelationalTypeMappingSourcePlugin win.
        var mapping = base.FindMapping(mappingInfo);
        if (mapping != null)
        {
            return mapping;
        }

        // A store type the dialect knows decides the CLR type; asked for one it doesn't hold, return nothing, so EF looks
        // for a value converter to one it does (uint over an "int" column reads an int, not a long).
        if (mappingInfo.StoreTypeName is { } named
            && _typeMappings.GetClrTypes(mappingInfo.StoreTypeNameBase ?? named) is { Count: > 0 } held)
        {
            var target = mappingInfo.ClrType ?? held[0];
            if (!held.Contains(target) || !_clrTypeMappings.TryGetValue(target, out mapping))
            {
                return null;
            }

            return WithStoreTypeFacets(mapping.WithTypeMappingInfo(mappingInfo), mappingInfo);
        }

        if (mappingInfo.ClrType is { } clrType
            && _clrTypeMappings.TryGetValue(clrType, out mapping))
        {
            if (mappingInfo.StoreTypeName is { } storeTypeName)
            {
                return WithStoreTypeFacets(mapping.WithStoreTypeAndSize(storeTypeName, mappingInfo.Size), mappingInfo);
            }

            // A precision without a scale means scale 0, not the default mapping's.
            mapping = mapping.WithTypeMappingInfo(mappingInfo);
            if (mappingInfo is { Precision: { } precision, Scale: null } && clrType == typeof(decimal))
            {
                mapping = mapping.WithPrecisionAndScale(precision, 0);
            }

            var facets = new StoreTypeFacets(
                mappingInfo.Size, mappingInfo.Precision, mappingInfo.Scale, mappingInfo.IsFixedLength == true, mappingInfo.IsUnicode != false);
            return _typeMappings.GetStoreType(clrType, facets) is { } sized
                ? mapping.WithStoreTypeAndSize(sized, mappingInfo.Size)
                : mapping;
        }

        if (mappingInfo.StoreTypeName is { } storeType
            && (_storeTypeMappings.TryGetValue(storeType, out mapping)
                || (mappingInfo.StoreTypeNameBase is { } storeTypeBase
                    && _storeTypeMappings.TryGetValue(storeTypeBase, out mapping))))
        {
            return WithStoreTypeFacets(mapping.WithStoreTypeAndSize(storeType, mappingInfo.Size), mappingInfo);
        }

        return null;
    }

    // A named decimal type binds with that type's own precision and scale, never the default mapping's (18, 2).
    private static RelationalTypeMapping WithStoreTypeFacets(RelationalTypeMapping mapping, in RelationalTypeMappingInfo mappingInfo)
    {
        if ((mapping.ClrType != typeof(decimal)) || (mappingInfo.StoreTypeName is not { } storeType))
        {
            return mapping;
        }

        var (precision, scale) = DecimalFacets(storeType);
        return mapping.WithPrecisionAndScale(precision, scale);
    }
}

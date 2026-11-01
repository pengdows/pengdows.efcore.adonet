using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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

        // A DateTime over a timestamptz column is sent as that type, so a UTC value isn't refused as timestamp.
        if (mappingInfo.ClrType == typeof(DateTime) && IsStoreType(mappingInfo, "timestamp with time zone"))
        {
            return new PostgreSqlTimestampWithTimeZoneMapping(mappingInfo.StoreTypeName!, services.GetRequiredService<ITypeMappingDialect>());
        }

        // A text value over a json, jsonb or xml column: PostgreSQL types text by the column only when the parameter names its type.
        if (mappingInfo.ClrType == typeof(string)
            && mappingInfo.StoreTypeNameBase is { } textStore
            && (IsStoreType(mappingInfo, "json") || IsStoreType(mappingInfo, "jsonb") || IsStoreType(mappingInfo, "xml")))
        {
            return new PostgreSqlTextTypedMapping(textStore);
        }

        if (mappingInfo.StoreTypeName is null && mappingInfo.ClrType is { } defaultClr
            && DefaultStoreTypes.TryGetValue(defaultClr, out var defaultStore))
        {
            return new PostgreSqlNamedTypeMapping(defaultClr, defaultStore);
        }

        // A value PostgreSQL won't take as text: the parameter names the column's type. Bound as its CLR type, as pengdows.crud
        // does (MAC addresses bind PhysicalAddress; Npgsql rejects a string for macaddr, and cidr needs a network value).
        if (mappingInfo.StoreTypeNameBase is { } namedStore
            && NamedTypeClr(mappingInfo.ClrType, namedStore) is { } namedClr)
        {
            return new PostgreSqlNamedTypeMapping(namedClr, mappingInfo.StoreTypeName ?? namedStore);
        }

        if (mappingInfo.ClrType is { IsArray: true } arrayType
            && arrayType != typeof(byte[])
            && mappingInfo.StoreTypeName is { } arrayStore
            && arrayStore.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementType = arrayType.GetElementType()!;
            // Range and text-typed elements are built here: asking the source while the model is finalizing fails.
            var element = ElementMapping(elementType, arrayStore[..^2])
                ?? services.GetRequiredService<IRelationalTypeMappingSource>().FindMapping(elementType, arrayStore[..^2]);
            return element == null ? null : new PostgreSqlArrayTypeMapping(arrayType, arrayStore, element);
        }

        return null;
    }

    private static RelationalTypeMapping? ElementMapping(Type elementType, string storeType)
    {
        if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(NpgsqlRange<>)
            && storeType.EndsWith("range", StringComparison.OrdinalIgnoreCase))
        {
            return new PostgreSqlProviderTypeMapping(elementType, storeType);
        }

        if (NamedTypeClr(elementType, storeType) is { } namedElement)
        {
            return new PostgreSqlNamedTypeMapping(namedElement, storeType);
        }

        return elementType == typeof(string) && storeType is "json" or "jsonb" or "xml"
            ? new PostgreSqlTextTypedMapping(storeType)
            : null;
    }

    // PostgreSQL types the parameter must name (its DataTypeName) and bind as their CLR value, because the text form is
    // refused or typed differently: the network, geometric, text-search, bit and identifier types, and the multirange types,
    // which are arrays of their range type. Each entry is the CLR type Npgsql reads and writes for the store type.
    private static readonly Dictionary<string, Type> NamedStoreTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["macaddr"] = typeof(PhysicalAddress),
        ["macaddr8"] = typeof(PhysicalAddress),
        ["cidr"] = typeof(IPNetwork),
        ["xid8"] = typeof(ulong),
        ["jsonpath"] = typeof(string),
        ["point"] = typeof(NpgsqlPoint),
        ["lseg"] = typeof(NpgsqlLSeg),
        ["path"] = typeof(NpgsqlPath),
        ["box"] = typeof(NpgsqlBox),
        ["line"] = typeof(NpgsqlLine),
        ["circle"] = typeof(NpgsqlCircle),
        ["polygon"] = typeof(NpgsqlPolygon),
        ["pg_lsn"] = typeof(NpgsqlLogSequenceNumber),
        ["tsvector"] = typeof(NpgsqlTsVector),
        ["tsquery"] = typeof(NpgsqlTsQuery),
        ["money"] = typeof(decimal),
        ["time with time zone"] = typeof(DateTimeOffset),
        ["tid"] = typeof(NpgsqlTid),
        ["oid"] = typeof(uint),
        ["regtype"] = typeof(uint),
        ["regconfig"] = typeof(uint),
        ["cid"] = typeof(uint),
        ["bit"] = typeof(BitArray),
        ["bit varying"] = typeof(BitArray),
        ["int4multirange"] = typeof(NpgsqlRange<int>[]),
        ["int8multirange"] = typeof(NpgsqlRange<long>[]),
        ["nummultirange"] = typeof(NpgsqlRange<decimal>[]),
        ["datemultirange"] = typeof(NpgsqlRange<DateOnly>[]),
        ["tsmultirange"] = typeof(NpgsqlRange<DateTime>[]),
        ["tstzmultirange"] = typeof(NpgsqlRange<DateTime>[]),
    };

    // The one PostgreSQL type a CLR type has, used when the model names no store type (a collection's element, for one).
    // CLR types with more than one PostgreSQL type (uint, decimal, DateTime ranges) are left out: they need a store type.
    // BitArray defaults to the varying form, which holds any length.
    private static readonly Dictionary<Type, string> DefaultStoreTypes = new()
    {
        [typeof(NpgsqlPoint)] = "point",
        [typeof(NpgsqlLSeg)] = "lseg",
        [typeof(NpgsqlPath)] = "path",
        [typeof(NpgsqlBox)] = "box",
        [typeof(NpgsqlLine)] = "line",
        [typeof(NpgsqlCircle)] = "circle",
        [typeof(NpgsqlPolygon)] = "polygon",
        [typeof(NpgsqlTsVector)] = "tsvector",
        [typeof(NpgsqlTsQuery)] = "tsquery",
        [typeof(NpgsqlTid)] = "tid",
        [typeof(NpgsqlLogSequenceNumber)] = "pg_lsn",
        [typeof(IPNetwork)] = "cidr",
        [typeof(PhysicalAddress)] = "macaddr",
        [typeof(BitArray)] = "bit varying",
        [typeof(NpgsqlRange<int>)] = "int4range",
        [typeof(NpgsqlRange<long>)] = "int8range",
        [typeof(NpgsqlRange<decimal>)] = "numrange",
        [typeof(NpgsqlRange<DateOnly>)] = "daterange",
        [typeof(NpgsqlRange<DateTime>)] = "tsrange",
        [typeof(NpgsqlRange<DateTimeOffset>)] = "tstzrange",
    };

    // The CLR type a PostgreSQL type binds as, when the parameter must name the type: null when the pair isn't one of these.
    private static Type? NamedTypeClr(Type? clrType, string storeType)
        => clrType is not null && NamedStoreTypes.TryGetValue(storeType, out var named) && named == clrType ? clrType : null;

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

    // A range literal is its text, cast to the range type: '[1,5)'::int4range.
    protected override string GenerateNonNullSqlLiteral(object value)
        => value switch
        {
            IPAddress address => "'" + address + "'::" + StoreType,
            _ when value.GetType() is { IsGenericType: true } range && range.GetGenericTypeDefinition() == typeof(NpgsqlRange<>)
                => RangeLiteral(value),
            _ => throw new NotSupportedException("PostgreSQL provider-specific literals require their native type syntax."),
        };

    private string RangeLiteral(object range)
    {
        var type = range.GetType();
        object? Property(string name) => type.GetProperty(name)!.GetValue(range);
        if ((bool)Property(nameof(NpgsqlRange<int>.IsEmpty))!)
        {
            return "'empty'::" + StoreType;
        }

        var lowerInfinite = (bool)Property(nameof(NpgsqlRange<int>.LowerBoundInfinite))!;
        var upperInfinite = (bool)Property(nameof(NpgsqlRange<int>.UpperBoundInfinite))!;
        var open = lowerInfinite || !(bool)Property(nameof(NpgsqlRange<int>.LowerBoundIsInclusive))! ? "(" : "[";
        var close = upperInfinite || !(bool)Property(nameof(NpgsqlRange<int>.UpperBoundIsInclusive))! ? ")" : "]";
        var lower = lowerInfinite ? "" : RangeBound(Property(nameof(NpgsqlRange<int>.LowerBound)));
        var upper = upperInfinite ? "" : RangeBound(Property(nameof(NpgsqlRange<int>.UpperBound)));
        return "'" + open + lower + "," + upper + close + "'::" + StoreType;
    }

    private static string RangeBound(object? bound)
        => bound switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime time => time.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
            _ => Convert.ToString(bound, CultureInfo.InvariantCulture) ?? ""
        };
}

// Npgsql sends a UTC DateTime as timestamp without time zone unless the parameter names timestamptz.
internal sealed class PostgreSqlTimestampWithTimeZoneMapping : AdoNetDateTimeTypeMapping
{
    private readonly ITypeMappingDialect _mappingDialect;

    public PostgreSqlTimestampWithTimeZoneMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType, dialect)
        => _mappingDialect = dialect;

    private PostgreSqlTimestampWithTimeZoneMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters, dialect)
        => _mappingDialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlTimestampWithTimeZoneMapping(parameters, _mappingDialect);

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if (parameter is NpgsqlParameter npgsql)
        {
            npgsql.DataTypeName = StoreType;
        }
    }
}

// Names its store type on the parameter (Npgsql's DataTypeName) and binds the CLR value as it is: for types whose text form
// PostgreSQL rejects (macaddr, cidr, xid8, jsonpath).
internal sealed class PostgreSqlNamedTypeMapping : RelationalTypeMapping
{
    public PostgreSqlNamedTypeMapping(Type clrType, string storeType)
        : this(new RelationalTypeMappingParameters(new CoreTypeMappingParameters(clrType), storeType))
    {
    }

    private PostgreSqlNamedTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlNamedTypeMapping(parameters);

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if (parameter is NpgsqlParameter npgsql)
        {
            npgsql.DataTypeName = StoreType;
        }
    }

    // The value's text, quoted and cast to the store type; a bit string is PostgreSQL's B'...' literal.
    protected override string GenerateNonNullSqlLiteral(object value)
        => value is BitArray bits
            ? "B'" + string.Concat(bits.Cast<bool>().Select(bit => bit ? "1" : "0")) + "'"
            : "'" + (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").Replace("'", "''", StringComparison.Ordinal)
                + "'::" + StoreType;
}

// Names its store type on the parameter (Npgsql's DataTypeName), so PostgreSQL takes the text as that type.
internal sealed class PostgreSqlTextTypedMapping : RelationalTypeMapping
{
    public PostgreSqlTextTypedMapping(string storeType)
        : this(new RelationalTypeMappingParameters(new CoreTypeMappingParameters(typeof(string)), storeType))
    {
    }

    private PostgreSqlTextTypedMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlTextTypedMapping(parameters);

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if (parameter is NpgsqlParameter npgsql)
        {
            npgsql.DataTypeName = StoreType;
        }
    }

    protected override string GenerateNonNullSqlLiteral(object value)
        => "'" + ((string)value).Replace("'", "''", StringComparison.Ordinal) + "'::" + StoreType;
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

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if (parameter is NpgsqlParameter npgsql)
        {
            npgsql.DataTypeName = StoreType;
        }
    }

    protected override string GenerateNonNullSqlLiteral(object value)
        => "ARRAY[" + string.Join(", ", ((Array)value).Cast<object>().Select(v => ((RelationalTypeMapping)ElementTypeMapping!).GenerateSqlLiteral(v))) + "]";
}

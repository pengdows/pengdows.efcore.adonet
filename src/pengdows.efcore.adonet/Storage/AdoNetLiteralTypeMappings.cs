using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

// EF's stock mappings, each writing its constants through ITypeMappingDialect.FormatLiteral. Public, with a static Default,
// because EF's compiled-model generator emits `<MappingType>.Default` into the user's assembly.

public class AdoNetByteTypeMapping : ByteTypeMapping
{
    public static new AdoNetByteTypeMapping Default { get; } = new("SMALLINT", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetByteTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetByteTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetByteTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));

}

public class AdoNetShortTypeMapping : ShortTypeMapping
{
    public static new AdoNetShortTypeMapping Default { get; } = new("SMALLINT", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetShortTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetShortTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetShortTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetIntTypeMapping : IntTypeMapping
{
    public static new AdoNetIntTypeMapping Default { get; } = new("INTEGER", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetIntTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetIntTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetIntTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetLongTypeMapping : LongTypeMapping
{
    public static new AdoNetLongTypeMapping Default { get; } = new("BIGINT", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetLongTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetLongTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetLongTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetFloatTypeMapping : FloatTypeMapping
{
    public static new AdoNetFloatTypeMapping Default { get; } = new("REAL", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetFloatTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetFloatTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetFloatTypeMapping(parameters, _dialect);

    // The float's exact value as a double: a database compares a REAL column with a numeric literal as a double (or keeps
    // floats in 8 bytes), and 0.1f is 0.10000000149011612 there, not 0.1.
    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, ((double)(float)value).ToString("R", CultureInfo.InvariantCulture));
}

public class AdoNetDoubleTypeMapping : DoubleTypeMapping
{
    public static new AdoNetDoubleTypeMapping Default { get; } = new("DOUBLE PRECISION", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetDoubleTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetDoubleTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetDoubleTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

/// <summary>Also binds the column's precision and scale, so the parameter's type doesn't vary with its value.</summary>
public class AdoNetDecimalTypeMapping : DecimalTypeMapping
{
    public static new AdoNetDecimalTypeMapping Default { get; } = new("DECIMAL(18,2)", AnsiTypeMappings.Instance, 18, 2);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetDecimalTypeMapping(string storeType, ITypeMappingDialect dialect, int? precision = null, int? scale = null)
        : base(storeType, precision: precision, scale: scale)
        => _dialect = dialect;

    protected AdoNetDecimalTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetDecimalTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));

    protected override void ConfigureParameter(DbParameter parameter)
    {
        // DbParameter's facets are bytes; a database's larger precision (some numerics go to 1000) is left to it.
        if (Precision is { } precision and <= byte.MaxValue)
        {
            parameter.Precision = (byte)precision;
        }

        if (Scale is { } scale and <= byte.MaxValue)
        {
            parameter.Scale = (byte)scale;
        }
    }
}

public class AdoNetDateTimeTypeMapping : DateTimeTypeMapping
{
    public static new AdoNetDateTimeTypeMapping Default { get; } = new("TIMESTAMP", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetDateTimeTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType, dialect.DateTimeDbType)
        => _dialect = dialect;

    protected AdoNetDateTimeTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetDateTimeTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);

        // TIMESTAMP WITHOUT TIME ZONE represents a wall-clock value. Drivers such as Npgsql
        // reject a UTC DateTime for that store type, even though the underlying instant is not
        // part of the SQL value. Normalize only the Kind marker before binding.
        if (parameter.Value is DateTime value && StoreType.Contains("WITHOUT TIME ZONE", StringComparison.OrdinalIgnoreCase))
        {
            parameter.Value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        }
    }
}

public class AdoNetDateTimeOffsetTypeMapping : DateTimeOffsetTypeMapping
{
    public static new AdoNetDateTimeOffsetTypeMapping Default { get; } = new("TIMESTAMP WITH TIME ZONE", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetDateTimeOffsetTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetDateTimeOffsetTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetDateTimeOffsetTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetDateOnlyTypeMapping : DateOnlyTypeMapping
{
    public static new AdoNetDateOnlyTypeMapping Default { get; } = new("DATE", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetDateOnlyTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetDateOnlyTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetDateOnlyTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetTimeOnlyTypeMapping : TimeOnlyTypeMapping
{
    public static new AdoNetTimeOnlyTypeMapping Default { get; } = new("TIME", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetTimeOnlyTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetTimeOnlyTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetTimeOnlyTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetTimeSpanTypeMapping : TimeSpanTypeMapping
{
    public static new AdoNetTimeSpanTypeMapping Default { get; } = new("INTERVAL", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetTimeSpanTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetTimeSpanTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetTimeSpanTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetGuidTypeMapping : GuidTypeMapping
{
    public static new AdoNetGuidTypeMapping Default { get; } = new("UUID", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetGuidTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetGuidTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetGuidTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetByteArrayTypeMapping : ByteArrayTypeMapping
{
    public static new AdoNetByteArrayTypeMapping Default { get; } = new("VARBINARY", AnsiTypeMappings.Instance);

    private readonly ITypeMappingDialect _dialect;

    public AdoNetByteArrayTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetByteArrayTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetByteArrayTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

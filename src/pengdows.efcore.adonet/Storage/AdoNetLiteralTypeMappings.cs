using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

// EF's stock mappings, each writing its constants through ITypeMappingDialect.FormatLiteral.

public class AdoNetByteTypeMapping : ByteTypeMapping
{
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
    private readonly ITypeMappingDialect _dialect;

    public AdoNetFloatTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType)
        => _dialect = dialect;

    protected AdoNetFloatTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetFloatTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetDoubleTypeMapping : DoubleTypeMapping
{
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
    private readonly ITypeMappingDialect _dialect;

    public AdoNetDateTimeTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType, System.Data.DbType.DateTime2)
        => _dialect = dialect;

    protected AdoNetDateTimeTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters)
        => _dialect = dialect;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetDateTimeTypeMapping(parameters, _dialect);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _dialect.FormatLiteral(value, StoreType, base.GenerateNonNullSqlLiteral(value));
}

public class AdoNetDateTimeOffsetTypeMapping : DateTimeOffsetTypeMapping
{
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

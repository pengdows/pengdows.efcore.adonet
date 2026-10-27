using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Binds through <see cref="DbType" />: <c>String</c>/<c>AnsiString</c>, or their fixed-length forms for fixed-length
///     columns. Constants are written by the dialect's <see cref="ISqlDialect.StringLiteral" />.
/// </summary>
public class AdoNetStringTypeMapping : StringTypeMapping
{
    public static new AdoNetStringTypeMapping Default { get; } = new("VARCHAR", AnsiSqlDialect.Instance, AnsiTypeMappings.Instance);

    private readonly ISqlDialect _sql;
    private readonly ITypeMappingDialect _typeMappings;

    public AdoNetStringTypeMapping(
        string storeType,
        ISqlDialect sql,
        ITypeMappingDialect typeMappings,
        bool unicode = true,
        int? size = null)
        : base(storeType, GetDbType(unicode, fixedLength: false), unicode, size)
    {
        _sql = sql;
        _typeMappings = typeMappings;
    }

    protected AdoNetStringTypeMapping(RelationalTypeMappingParameters parameters, ISqlDialect sql, ITypeMappingDialect typeMappings)
        : base(
            new RelationalTypeMappingParameters(
                parameters.CoreParameters,
                parameters.StoreType,
                parameters.StoreTypePostfix,
                GetDbType(parameters.Unicode, parameters.FixedLength),
                parameters.Unicode,
                parameters.Size,
                parameters.FixedLength,
                parameters.Precision,
                parameters.Scale))
    {
        _sql = sql;
        _typeMappings = typeMappings;
    }

    private static DbType GetDbType(bool unicode, bool fixedLength)
        => (unicode, fixedLength) switch
        {
            (true, true) => System.Data.DbType.StringFixedLength,
            (true, false) => System.Data.DbType.String,
            (false, true) => System.Data.DbType.AnsiStringFixedLength,
            (false, false) => System.Data.DbType.AnsiString
        };

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetStringTypeMapping(parameters, _sql, _typeMappings);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _sql.StringLiteral((string)value);

    /// <summary>
    ///     The column's size, or the dialect's size for unbounded columns, when the value fits; otherwise the driver's. A
    ///     fixed-length parameter stays fixed only when the value fills it, so the driver neither pads nor truncates.
    /// </summary>
    /// <remarks>Follows EF Core's SQL Server string mapping (MIT, .NET Foundation).</remarks>
    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);

        var length = (parameter.Value as string)?.Length;
        var size = Size is > 0 ? Size : _typeMappings.StringParameterSize(IsUnicode);
        var fixedLength = IsFixedLength && (length == null || length == Size);

        // This override owns parameter configuration, so preserve the DbType that the base mapping
        // would otherwise apply before handling value-sensitive fixed-length behavior.
        parameter.DbType = GetDbType(IsUnicode, fixedLength);

        if (size != null && (length == null || length <= size))
        {
            parameter.Size = size.Value;
        }
    }
}

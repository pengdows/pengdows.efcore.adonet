using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Storage;
using NpgsqlTypes;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     A TimeSpan over an interval, read as Npgsql's NpgsqlInterval: an interval holding months, which have no fixed length,
///     is refused. Npgsql's own TimeSpan reading refuses positive months but reads negative ones as TimeSpan.Zero
///     (https://github.com/npgsql/npgsql/pull/6682).
/// </summary>
internal sealed class PostgreSqlIntervalTypeMapping : AdoNetTimeSpanTypeMapping
{
    private static readonly MethodInfo GetInterval
        = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!.MakeGenericMethod(typeof(NpgsqlInterval));

    private static readonly MethodInfo ToTimeSpanMethod
        = typeof(PostgreSqlIntervalTypeMapping).GetMethod(nameof(ToTimeSpan), BindingFlags.NonPublic | BindingFlags.Static)!;

    public PostgreSqlIntervalTypeMapping(string storeType, ITypeMappingDialect dialect)
        : base(storeType, dialect)
    {
    }

    private PostgreSqlIntervalTypeMapping(RelationalTypeMappingParameters parameters, ITypeMappingDialect dialect)
        : base(parameters, dialect)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlIntervalTypeMapping(parameters, TypeMappings);

    public override MethodInfo GetDataReaderMethod()
        => GetInterval;

    public override Expression CustomizeDataReaderExpression(Expression expression)
        => Expression.Call(ToTimeSpanMethod, expression);

    private static TimeSpan ToTimeSpan(NpgsqlInterval interval)
        => interval.Months != 0
            ? throw new InvalidCastException(
                $"An interval of {interval.Months} months, {interval.Days} days and {interval.Time} microseconds has no TimeSpan: "
                + "months have no fixed length.")
            : TimeSpan.FromDays(interval.Days) + TimeSpan.FromTicks(interval.Time * (TimeSpan.TicksPerMillisecond / 1000));
}

/// <summary>Maps TimeSpan to <see cref="PostgreSqlIntervalTypeMapping" /> wherever it is an interval.</summary>
internal sealed class PostgreSqlIntervalTypeMappingPlugin(ITypeMappingDialect typeMappings) : IRelationalTypeMappingSourcePlugin
{
    public RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
        => (mappingInfo.ClrType == typeof(TimeSpan))
            && (typeMappings.GetStoreType(typeof(TimeSpan)) is { } interval)
            && ((mappingInfo.StoreTypeNameBase ?? interval).Equals(interval, StringComparison.OrdinalIgnoreCase))
                ? new PostgreSqlIntervalTypeMapping(interval, typeMappings)
                : null;
}

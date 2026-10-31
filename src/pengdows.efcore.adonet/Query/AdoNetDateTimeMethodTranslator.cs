using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
namespace pengdows.efcore.adonet.Query;

/// <summary>
///     Date and time arithmetic and conversions. Hours, minutes, seconds, milliseconds and fractional days are added as
///     microseconds, so a fraction is kept rather than rounded to the unit.
/// </summary>
internal class AdoNetDateTimeMethodTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    FunctionSyntax functions) : IMethodCallTranslator
{
    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        var type = method.DeclaringType;
        if ((type != typeof(DateTime)) && (type != typeof(DateTimeOffset)) && (type != typeof(DateOnly)) && (type != typeof(TimeOnly)))
        {
            return null;
        }

        if (instance == null)
        {
            var conversion = (type, method.Name, arguments.Count == 1 ? arguments[0].Type : null) switch
            {
                (_, nameof(DateOnly.FromDateTime), var from) when type == typeof(DateOnly) && from == typeof(DateTime)
                    => functions.DateFromDateTime,
                (_, nameof(TimeOnly.FromDateTime), var from) when type == typeof(TimeOnly) && from == typeof(DateTime)
                    => functions.TimeFromDateTime,
                (_, nameof(TimeOnly.FromTimeSpan), var from) when type == typeof(TimeOnly) && from == typeof(TimeSpan)
                    => functions.TimeFromTimeSpan,
                _ => null
            };
            return conversion == null
                ? null
                : sqlExpressionFactory.Call(
                    conversion, [sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!], method.ReturnType,
                    typeMappingSource.FindMapping(method.ReturnType));
        }

        if ((type == typeof(DateTimeOffset)) && (arguments.Count == 0))
        {
            var unix = method.Name switch
            {
                nameof(DateTimeOffset.ToUnixTimeSeconds) => functions.UnixSeconds,
                nameof(DateTimeOffset.ToUnixTimeMilliseconds) => functions.UnixMilliseconds,
                _ => null
            };
            return sqlExpressionFactory.Call(unix, [instance], typeof(long), typeMappingSource.FindMapping(typeof(long)));
        }

        if (arguments.Count == 1 && (arguments[0].Type == typeof(double)))
        {
            var microseconds = method.Name switch
            {
                nameof(DateTime.AddDays) => 86_400_000_000.0,
                nameof(DateTime.AddHours) => 3_600_000_000.0,
                nameof(DateTime.AddMinutes) => 60_000_000.0,
                nameof(DateTime.AddSeconds) => 1_000_000.0,
                nameof(DateTime.AddMilliseconds) => 1_000.0,
                nameof(DateTime.AddMicroseconds) => 1.0,
                _ => (double?)null
            };
            if (microseconds is not { } factor)
            {
                return null;
            }

            var amount = sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!;
            return Same(
                functions.AddMicroseconds,
                instance,
                factor == 1.0 ? amount : sqlExpressionFactory.Multiply(amount, sqlExpressionFactory.Constant(factor, amount.TypeMapping)));
        }

        return (method.Name, arguments.Count) switch
        {
            (nameof(DateTime.AddYears), 1) => Same(functions.AddYears, instance, Default(arguments[0])),
            (nameof(DateTime.AddMonths), 1) => Same(functions.AddMonths, instance, Default(arguments[0])),
            (nameof(DateOnly.AddDays), 1) => Same(functions.AddDays, instance, Default(arguments[0])),
            (nameof(TimeOnly.Add), 1) when type == typeof(TimeOnly) => Same(functions.AddTime, instance, Default(arguments[0])),
            (nameof(TimeOnly.IsBetween), 2) when type == typeof(TimeOnly)
                => sqlExpressionFactory.Call(
                    functions.TimeIsBetween,
                    [instance, sqlExpressionFactory.ApplyTypeMapping(arguments[0], instance.TypeMapping)!,
                        sqlExpressionFactory.ApplyTypeMapping(arguments[1], instance.TypeMapping)!],
                    typeof(bool),
                    typeMappingSource.FindMapping(typeof(bool))),
            (nameof(DateOnly.ToDateTime), 1) when type == typeof(DateOnly) && arguments[0].Type == typeof(TimeOnly)
                => sqlExpressionFactory.Call(
                    functions.DateAtTime, [instance, Default(arguments[0])], typeof(DateTime), typeMappingSource.FindMapping(typeof(DateTime))),
            _ => null
        };
    }

    private SqlExpression Default(SqlExpression argument)
        => sqlExpressionFactory.ApplyDefaultTypeMapping(argument)!;

    // A result of the instance's own type and mapping.
    private SqlExpression? Same(string? template, SqlExpression instance, SqlExpression argument)
        => sqlExpressionFactory.Call(template, [instance, argument], instance.Type, instance.TypeMapping);
}

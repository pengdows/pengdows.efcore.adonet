using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational translators, then the core's, which write SQL through the dialect's <see cref="FunctionSyntax" />.</summary>
internal class AdoNetMethodCallTranslatorProvider : RelationalMethodCallTranslatorProvider
{
    public AdoNetMethodCallTranslatorProvider(RelationalMethodCallTranslatorProviderDependencies dependencies, ISqlDialect dialect)
        : base(dependencies)
    {
        var sqlExpressionFactory = dependencies.SqlExpressionFactory;
        AddTranslators(
        [
            new AdoNetStringMethodTranslator(sqlExpressionFactory, dependencies.RelationalTypeMappingSource, dialect.Functions),
            new AdoNetMathTranslator(sqlExpressionFactory, dependencies.RelationalTypeMappingSource, dialect.Functions),
            new AdoNetDateTimeMethodTranslator(sqlExpressionFactory, dependencies.RelationalTypeMappingSource, dialect.Functions),
            new AdoNetMiscellaneousTranslator(sqlExpressionFactory, dependencies.RelationalTypeMappingSource, dialect.Functions),
            new AdoNetDbFunctionsTranslator(sqlExpressionFactory, dependencies.RelationalTypeMappingSource, dialect.Functions)
        ]);
    }
}

internal class AdoNetMemberTranslatorProvider : RelationalMemberTranslatorProvider
{
    public AdoNetMemberTranslatorProvider(
        RelationalMemberTranslatorProviderDependencies dependencies,
        IRelationalTypeMappingSource typeMappingSource,
        ISqlDialect dialect)
        : base(dependencies)
        => AddTranslators([new AdoNetMemberTranslator(dependencies.SqlExpressionFactory, typeMappingSource, dialect.Functions)]);
}



/// <summary><see cref="Math" />, <see cref="MathF" />, and the static math of <see cref="double" /> and <see cref="float" />.</summary>
internal class AdoNetMathTranslator(
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
        if ((instance != null) || ((type != typeof(Math)) && (type != typeof(MathF)) && (type != typeof(double)) && (type != typeof(float))))
        {
            return null;
        }

        var parameters = method.GetParameters();
        var template = (method.Name, parameters.Length) switch
        {
            (nameof(Math.Abs), 1) => functions.Abs,
            (nameof(Math.Floor), 1) => functions.Floor,
            (nameof(Math.Ceiling), 1) => functions.Ceiling,
            (nameof(Math.Round), 1) => functions.Round,
            (nameof(Math.Round), 2) when parameters[1].ParameterType == typeof(int) => functions.RoundDigits,
            (nameof(Math.Truncate), 1) => functions.Truncate,
            (nameof(Math.Sign), 1) => functions.Sign,
            (nameof(Math.Pow), 2) => functions.Power,
            (nameof(Math.Sqrt), 1) => functions.Sqrt,
            (nameof(Math.Exp), 1) => functions.Exp,
            (nameof(Math.Log), 1) => functions.Ln,
            (nameof(Math.Log), 2) => functions.LogBase,
            (nameof(Math.Log10), 1) => functions.Log10,
            (nameof(Math.Log2), 1) => functions.Log2,
            (nameof(Math.Acos), 1) => functions.Acos,
            (nameof(Math.Asin), 1) => functions.Asin,
            (nameof(Math.Atan), 1) => functions.Atan,
            (nameof(Math.Atan2), 2) => functions.Atan2,
            (nameof(Math.Cos), 1) => functions.Cos,
            (nameof(Math.Sin), 1) => functions.Sin,
            (nameof(Math.Tan), 1) => functions.Tan,
            (nameof(Math.Acosh), 1) => functions.Acosh,
            (nameof(Math.Asinh), 1) => functions.Asinh,
            (nameof(Math.Atanh), 1) => functions.Atanh,
            (nameof(Math.Cosh), 1) => functions.Cosh,
            (nameof(Math.Sinh), 1) => functions.Sinh,
            (nameof(Math.Tanh), 1) => functions.Tanh,
            (nameof(double.DegreesToRadians), 1) => functions.Radians,
            (nameof(double.RadiansToDegrees), 1) => functions.Degrees,
            _ => null
        };
        if (template == null)
        {
            return null;
        }

        var value = sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!;
        var rest = arguments.Skip(1).Select(a => a.Type == value.Type
            ? sqlExpressionFactory.ApplyTypeMapping(a, value.TypeMapping)!
            : sqlExpressionFactory.ApplyDefaultTypeMapping(a)!);
        var mapping = method.ReturnType == value.Type ? value.TypeMapping : typeMappingSource.FindMapping(method.ReturnType);
        return sqlExpressionFactory.Call(template, [value, .. rest], method.ReturnType, mapping);
    }
}

/// <summary>
///     <c>string.Length</c>; the parts of <see cref="DateTime" />, <see cref="DateTimeOffset" />, <see cref="DateOnly" />,
///     <see cref="TimeOnly" /> and <see cref="TimeSpan" />; and the current date and time.
/// </summary>
internal class AdoNetMemberTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    FunctionSyntax functions) : IMemberTranslator
{
    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        var type = member.DeclaringType;
        if (instance == null)
        {
            var current = type == typeof(DateTime)
                ? member.Name switch
                {
                    nameof(DateTime.Now) => functions.Now,
                    nameof(DateTime.UtcNow) => functions.UtcNow,
                    nameof(DateTime.Today) => functions.Today,
                    _ => null
                }
                : type == typeof(DateTimeOffset) && member.Name is nameof(DateTimeOffset.Now) or nameof(DateTimeOffset.UtcNow)
                    ? functions.OffsetNow
                    : null;
            return sqlExpressionFactory.Call(current, [], returnType, Mapping(returnType));
        }

        var template = type == typeof(string)
            ? member.Name == nameof(string.Length) ? functions.Length : null
            : type == typeof(TimeSpan)
                ? member.Name switch
                {
                    nameof(TimeSpan.TotalDays) => functions.TimeSpanTotalDays,
                    nameof(TimeSpan.Hours) => functions.TimeSpanHours,
                    nameof(TimeSpan.Minutes) => functions.TimeSpanMinutes,
                    nameof(TimeSpan.Seconds) => functions.TimeSpanSeconds,
                    nameof(TimeSpan.Milliseconds) => functions.TimeSpanMilliseconds,
                    nameof(TimeSpan.Microseconds) => functions.TimeSpanMicroseconds,
                    nameof(TimeSpan.Nanoseconds) => functions.TimeSpanNanoseconds,
                    _ => null
                }
                : type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly)
                    ? member.Name switch
                    {
                        nameof(DateTime.Year) => functions.Year,
                        nameof(DateTime.Month) => functions.Month,
                        nameof(DateTime.Day) => functions.Day,
                        nameof(DateTime.Hour) => functions.Hour,
                        nameof(DateTime.Minute) => functions.Minute,
                        nameof(DateTime.Second) => functions.Second,
                        nameof(DateTime.Millisecond) => functions.Millisecond,
                        nameof(DateTime.Microsecond) => functions.Microsecond,
                        nameof(DateTime.Nanosecond) => functions.Nanosecond,
                        nameof(DateTime.DayOfYear) => functions.DayOfYear,
                        nameof(DateTime.DayOfWeek) => functions.DayOfWeek,
                        nameof(DateTime.Date) => functions.DateOf,
                        nameof(DateTime.TimeOfDay) => functions.TimeOfDay,
                        nameof(DateOnly.DayNumber) => functions.DayNumber,
                        _ => null
                    }
                    : null;

        if (type == typeof(DateTimeOffset))
        {
            template = functions.OffsetDateTime is { } offset ? template?.Replace("{0}", offset, StringComparison.Ordinal) : null;
        }

        return sqlExpressionFactory.Call(
            template, [instance], returnType, returnType == instance.Type ? instance.TypeMapping : Mapping(returnType));
    }

    // An enum, such as DayOfWeek, is read as its underlying integer.
    private RelationalTypeMapping? Mapping(Type type)
        => typeMappingSource.FindMapping(type.IsEnum ? Enum.GetUnderlyingType(type) : type);
}



/// <summary>EF.Functions.DateDiff*: the dialect's count of units, the two values given one type mapping.</summary>
internal class AdoNetDbFunctionsTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    FunctionSyntax functions) : IMethodCallTranslator
{
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((method.DeclaringType != typeof(AdoNetDbFunctionsExtensions))
            || !method.Name.StartsWith("DateDiff", StringComparison.Ordinal)
            || !Enum.TryParse<DateDiffUnit>(method.Name["DateDiff".Length..], out var unit)
            || (functions.DateDiff(unit) is not { } template))
        {
            return null;
        }

        var mapping = ExpressionExtensions.InferTypeMapping(arguments[1], arguments[2]);
        return sqlExpressionFactory.Call(
            template,
            [sqlExpressionFactory.ApplyTypeMapping(arguments[1], mapping)!, sqlExpressionFactory.ApplyTypeMapping(arguments[2], mapping)!],
            method.ReturnType,
            typeMappingSource.FindMapping(typeof(int)));
    }
}

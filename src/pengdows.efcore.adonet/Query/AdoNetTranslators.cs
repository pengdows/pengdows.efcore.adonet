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

/// <summary>String methods. .NET positions are 0-based and SQL's 1-based; the translator converts.</summary>
internal class AdoNetStringMethodTranslator(
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
        if (method.DeclaringType != typeof(string))
        {
            return null;
        }

        var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
        if (instance == null)
        {
            return method.Name == nameof(string.IsNullOrWhiteSpace) && parameters is [var type] && type == typeof(string)
                ? NullOrWhiteSpace(arguments[0])
                : null;
        }

        // A char is a string of one character, as SQL has no character type.
        var stringArguments = parameters.All(p => (p == typeof(string)) || (p == typeof(char)));
        var mapping = ExpressionExtensions.InferTypeMapping([instance, .. arguments.Where(a => a.Type == typeof(string))])
            ?? typeMappingSource.FindMapping(typeof(string));
        instance = sqlExpressionFactory.ApplyTypeMapping(instance, mapping)!;
        var args = arguments.Select(a => a switch
        {
            SqlConstantExpression { Value: char c } => sqlExpressionFactory.Constant(c.ToString(), mapping),
            _ when (a.Type == typeof(string)) || (a.Type == typeof(char)) => sqlExpressionFactory.ApplyTypeMapping(a, mapping)!,
            _ => sqlExpressionFactory.ApplyDefaultTypeMapping(a)!
        }).ToArray();

        // Trim, TrimStart and TrimEnd of one character, given alone or as the only one in an array.
        if ((method.Name is nameof(string.Trim) or nameof(string.TrimStart) or nameof(string.TrimEnd))
            && (arguments is [SqlConstantExpression { Value: char or char[] { Length: 1 } } trimmed]))
        {
            var character = trimmed.Value is char single ? single : ((char[])trimmed.Value!)[0];
            var template = method.Name switch
            {
                nameof(string.Trim) => functions.TrimCharacter,
                nameof(string.TrimStart) => functions.TrimStartCharacter,
                _ => functions.TrimEndCharacter
            };
            return String(template, instance, sqlExpressionFactory.Constant(character.ToString(), mapping));
        }

        // Trim, TrimStart and TrimEnd of several characters: the database's trim of a set, given the set as one string.
        if ((method.Name is nameof(string.Trim) or nameof(string.TrimStart) or nameof(string.TrimEnd))
            && (arguments is [SqlConstantExpression { Value: char[] { Length: > 1 } characters }]))
        {
            var template = method.Name switch
            {
                nameof(string.Trim) => functions.TrimCharacters,
                nameof(string.TrimStart) => functions.TrimStartCharacters,
                _ => functions.TrimEndCharacters
            };
            return String(template, instance, sqlExpressionFactory.Constant(functions.TrimCharacterSet(characters), mapping));
        }

        return (method.Name, parameters.Length, stringArguments) switch
        {
            (nameof(string.IndexOf), 2, false) when parameters[1] == typeof(int) && (parameters[0] == typeof(string) || parameters[0] == typeof(char))
                => Int(functions.PositionFrom, args[0], instance, OneBased(args[1])) is { } from
                    ? sqlExpressionFactory.Subtract(from, sqlExpressionFactory.Constant(1))
                    : null,
            (nameof(string.PadLeft), 1, false) when parameters[0] == typeof(int)
                => String(functions.PadLeft, instance, args[0], sqlExpressionFactory.Constant(" ", mapping)),
            (nameof(string.PadLeft), 2, false) when (parameters[0] == typeof(int)) && (parameters[1] == typeof(char))
                => String(functions.PadLeft, instance, args[0], args[1]),
            (nameof(string.PadRight), 1, false) when parameters[0] == typeof(int)
                => String(functions.PadRight, instance, args[0], sqlExpressionFactory.Constant(" ", mapping)),
            (nameof(string.PadRight), 2, false) when (parameters[0] == typeof(int)) && (parameters[1] == typeof(char))
                => String(functions.PadRight, instance, args[0], args[1]),
            (nameof(string.ToUpper), 0, _) => String(functions.Upper, instance),
            (nameof(string.ToLower), 0, _) => String(functions.Lower, instance),
            (nameof(string.Trim), 0, _) => String(functions.Trim, instance),
            (nameof(string.TrimStart), 0, _) => String(functions.TrimStart, instance),
            (nameof(string.TrimEnd), 0, _) => String(functions.TrimEnd, instance),
            (nameof(string.Substring), 1, false) => String(functions.SubstringFrom, instance, OneBased(args[0])),
            (nameof(string.Substring), 2, false) => String(functions.Substring, instance, OneBased(args[0]), args[1]),
            (nameof(string.Replace), 2, true) => String(functions.Replace, instance, args[0], args[1]),
            (nameof(string.IndexOf), 1, true) => Position(args[0], instance) is { } position
                ? sqlExpressionFactory.Subtract(position, sqlExpressionFactory.Constant(1))
                : null,
            (nameof(string.StartsWith), 1, true) => Match(instance, args[0], start: true, end: false),
            (nameof(string.EndsWith), 1, true) => Match(instance, args[0], start: false, end: true),
            (nameof(string.Contains), 1, true) => Match(instance, args[0], start: true, end: true),
            _ => null
        };
    }

    private SqlExpression? String(string? template, params SqlExpression[] arguments)
        => sqlExpressionFactory.Call(template, arguments, typeof(string), arguments[0].TypeMapping);

    private SqlExpression? Int(string? template, params SqlExpression[] arguments)
        => sqlExpressionFactory.Call(template, arguments, typeof(int), typeMappingSource.FindMapping(typeof(int)));

    private SqlExpression? Position(SqlExpression value, SqlExpression instance)
        => Int(functions.Position, value, instance);

    private SqlExpression OneBased(SqlExpression index)
        => index is SqlConstantExpression { Value: int value }
            ? sqlExpressionFactory.Constant(value + 1, index.TypeMapping)
            : sqlExpressionFactory.Add(index, sqlExpressionFactory.Constant(1));

    // TRIM removes spaces only, so a string of tabs or line breaks isn't white space here as it is in .NET; EF's own
    // providers translate it the same way.
    private SqlExpression? NullOrWhiteSpace(SqlExpression argument)
    {
        argument = sqlExpressionFactory.ApplyDefaultTypeMapping(argument)!;
        return String(functions.Trim, argument) is { } trimmed
            ? sqlExpressionFactory.OrElse(
                sqlExpressionFactory.IsNull(argument),
                sqlExpressionFactory.Equal(trimmed, sqlExpressionFactory.Constant("", argument.TypeMapping)))
            : null;
    }

    /// <summary>
    ///     A constant becomes a LIKE pattern, its wildcards escaped (parameters are made patterns by
    ///     <see cref="AdoNetSqlTranslatingExpressionVisitor" />). Anything else may itself contain wildcards, so it is
    ///     compared by position instead, as is a constant with wildcards where LIKE has no ESCAPE.
    /// </summary>
    private SqlExpression? Match(SqlExpression instance, SqlExpression value, bool start, bool end)
    {
        // A null constant matches nothing, and is never passed on to LENGTH or SUBSTRING.
        if (value is SqlConstantExpression { Value: null })
        {
            return sqlExpressionFactory.Constant(false);
        }

        if (value is SqlConstantExpression { Value: string constant } && functions.EscapeLikePattern(constant) is { } pattern)
        {
            return sqlExpressionFactory.Like(
                instance,
                sqlExpressionFactory.Constant((end ? "%" : "") + pattern + (start ? "%" : ""), instance.TypeMapping),
                pattern == constant ? null : sqlExpressionFactory.Constant(functions.LikeEscapeCharacter!.Value.ToString(), instance.TypeMapping));
        }

        if (start && end)
        {
            return Position(value, instance) is { } position
                ? sqlExpressionFactory.GreaterThan(position, sqlExpressionFactory.Constant(0))
                : null;
        }

        if (Int(functions.Length, value) is not { } valueLength)
        {
            return null;
        }

        var part = start
            ? String(functions.Substring, instance, sqlExpressionFactory.Constant(1), valueLength)
            : Int(functions.Length, instance) is { } length
                ? String(
                    functions.SubstringFrom,
                    instance,
                    sqlExpressionFactory.Add(sqlExpressionFactory.Subtract(length, valueLength), sqlExpressionFactory.Constant(1)))
                : null;

        // The argument may itself hold wildcards (a column), so this compares the text. A null on either side is false, and
        // its negation true, which EF's null compensation would otherwise turn into TRUE for a nullable column.
        return part == null
            ? null
            : sqlExpressionFactory.AndAlso(
                sqlExpressionFactory.IsNotNull(instance),
                sqlExpressionFactory.AndAlso(sqlExpressionFactory.IsNotNull(value), sqlExpressionFactory.Equal(part, value)));
    }
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

/// <summary>
///     <see cref="Guid.NewGuid" />, <see cref="System.Text.RegularExpressions.Regex.IsMatch(string, string)" />, the
///     <see cref="Convert" /> methods to scalar types, and <c>ToString()</c> of numbers and GUIDs, which are casts.
/// </summary>
internal class AdoNetMiscellaneousTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    FunctionSyntax functions) : IMethodCallTranslator
{
    private static readonly HashSet<Type> ConvertTargets =
        [typeof(bool), typeof(byte), typeof(decimal), typeof(double), typeof(short), typeof(int), typeof(long), typeof(string)];

    // Types whose invariant text a cast to text writes as .NET does.
    private static readonly HashSet<Type> TextualTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(decimal), typeof(Guid), typeof(string)
    ];

    private static readonly HashSet<Type> ConvertSources =
    [
        typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long),
        typeof(ulong), typeof(decimal), typeof(double), typeof(float), typeof(string)
    ];

    private static SqlExpression Unboxed(SqlExpression argument)
        => argument is SqlUnaryExpression { OperatorType: System.Linq.Expressions.ExpressionType.Convert, Type: var type, Operand: var operand }
            && (type == typeof(object))
                ? operand
                : argument;

    private static Type SourceType(SqlExpression argument)
    {
        var type = Unboxed(argument).Type;
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((method.DeclaringType == typeof(Guid)) && (method.Name == nameof(Guid.NewGuid)) && (arguments.Count == 0))
        {
            return sqlExpressionFactory.Call(functions.NewGuid, [], typeof(Guid), typeMappingSource.FindMapping(typeof(Guid)));
        }

        if ((method.DeclaringType == typeof(System.Text.RegularExpressions.Regex))
            && (method.Name == nameof(System.Text.RegularExpressions.Regex.IsMatch))
            && (instance == null)
            && method.GetParameters() is [{ ParameterType: var input }, { ParameterType: var pattern }]
            && (input == typeof(string))
            && (pattern == typeof(string)))
        {
            var mapping = ExpressionExtensions.InferTypeMapping(arguments[0], arguments[1]) ?? typeMappingSource.FindMapping(typeof(string));
            return sqlExpressionFactory.Call(
                functions.RegexIsMatch,
                [sqlExpressionFactory.ApplyTypeMapping(arguments[0], mapping)!, sqlExpressionFactory.ApplyTypeMapping(arguments[1], mapping)!],
                typeof(bool),
                typeMappingSource.FindMapping(typeof(bool)));
        }

        // Enumerable.First and Contains over a byte array column.
        if ((method.DeclaringType == typeof(Enumerable))
            && (instance == null)
            && (arguments.Count > 0)
            && (arguments[0].Type == typeof(byte[])))
        {
            var bytes = sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])!;
            var byteMapping = typeMappingSource.FindMapping(typeof(byte));
            return (method.Name, arguments.Count) switch
            {
                (nameof(Enumerable.First), 1)
                    => sqlExpressionFactory.Call(functions.ByteAt, [bytes, sqlExpressionFactory.Constant(1)], typeof(byte), byteMapping),
                (nameof(Enumerable.ElementAt), 2) when arguments[1].Type == typeof(int)
                    => sqlExpressionFactory.Call(
                        functions.ByteAt,
                        [bytes, sqlExpressionFactory.Add(sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[1])!, sqlExpressionFactory.Constant(1))],
                        typeof(byte),
                        byteMapping),
                (nameof(Enumerable.Contains), 2)
                    => sqlExpressionFactory.Call(
                        functions.BytesContain,
                        [bytes, sqlExpressionFactory.ApplyTypeMapping(arguments[1], byteMapping)!],
                        typeof(bool),
                        typeMappingSource.FindMapping(typeof(bool))),
                _ => null
            };
        }

        // A value boxed to object (Convert.ToInt32((object)x)) converts as the value it holds.
        if ((method.DeclaringType == typeof(Convert))
            && (instance == null)
            && (arguments.Count == 1)
            && ConvertTargets.Contains(method.ReturnType)
            && (ConvertSources.Contains(SourceType(arguments[0]))
                // A date as text: the database's format, where .NET's is the current culture's.
                || ((method.ReturnType == typeof(string)) && (SourceType(arguments[0]) == typeof(DateTime)))))
        {
            return sqlExpressionFactory.Convert(
                sqlExpressionFactory.ApplyDefaultTypeMapping(Unboxed(arguments[0]))!,
                method.ReturnType,
                typeMappingSource.FindMapping(method.ReturnType));
        }

        if ((instance != null)
            && (method.Name == nameof(ToString))
            && (arguments.Count == 0)
            && TextualTypes.Contains(Nullable.GetUnderlyingType(instance.Type) ?? instance.Type))
        {
            return instance.Type == typeof(string)
                ? instance
                : sqlExpressionFactory.Convert(instance, typeof(string), typeMappingSource.FindMapping(typeof(string)));
        }

        return null;
    }
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

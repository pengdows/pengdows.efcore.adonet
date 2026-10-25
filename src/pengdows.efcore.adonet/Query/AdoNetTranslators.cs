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
            new AdoNetMathTranslator(sqlExpressionFactory, dialect.Functions)
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

        var stringArguments = parameters.All(p => p == typeof(string));
        var mapping = ExpressionExtensions.InferTypeMapping([instance, .. stringArguments ? arguments : []])
            ?? typeMappingSource.FindMapping(typeof(string));
        instance = sqlExpressionFactory.ApplyTypeMapping(instance, mapping)!;
        var args = arguments.Select(a => a.Type == typeof(string)
            ? sqlExpressionFactory.ApplyTypeMapping(a, mapping)!
            : sqlExpressionFactory.ApplyDefaultTypeMapping(a)!).ToArray();

        return (method.Name, parameters.Length, stringArguments) switch
        {
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

        return part == null ? null : sqlExpressionFactory.Equal(part, value);
    }
}

internal class AdoNetMathTranslator(ISqlExpressionFactory sqlExpressionFactory, FunctionSyntax functions) : IMethodCallTranslator
{
    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(Math))
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
            (nameof(Math.Pow), 2) => functions.Power,
            (nameof(Math.Sqrt), 1) => functions.Sqrt,
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
        return sqlExpressionFactory.Call(template, [value, .. rest], method.ReturnType, value.TypeMapping);
    }
}

/// <summary><c>string.Length</c>, and the date parts of <see cref="DateTime" />, <see cref="DateTimeOffset" /> and <see cref="DateOnly" />.</summary>
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
        if (instance == null)
        {
            return null;
        }

        var type = member.DeclaringType;
        var template = type == typeof(string)
            ? member.Name == nameof(string.Length) ? functions.Length : null
            : type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly)
                ? member.Name switch
                {
                    nameof(DateTime.Year) => functions.Year,
                    nameof(DateTime.Month) => functions.Month,
                    nameof(DateTime.Day) => functions.Day,
                    nameof(DateTime.Hour) => functions.Hour,
                    nameof(DateTime.Minute) => functions.Minute,
                    nameof(DateTime.Second) => functions.Second,
                    _ => null
                }
                : null;

        if (type == typeof(DateTimeOffset))
        {
            template = functions.OffsetDateTime is { } offset ? template?.Replace("{0}", offset, StringComparison.Ordinal) : null;
        }

        return sqlExpressionFactory.Call(template, [instance], typeof(int), typeMappingSource.FindMapping(typeof(int)));
    }
}

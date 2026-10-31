using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
namespace pengdows.efcore.adonet.Query;

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

using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     A function written from a <see cref="FunctionSyntax" /> template. It is an ordinary <see cref="SqlFunctionExpression" />
///     whose name carries the template, so EF's null semantics and caching treat it like any built-in function; only the
///     SQL generator knows to write it out. Plugins may use it for their own translators.
/// </summary>
public static class SqlTemplate
{
    private const string Prefix = "template:";
    private const string ConditionPrefix = "condition:";

    /// <summary>Null when the dialect has no <paramref name="template" />, which leaves the member untranslated.</summary>
    public static SqlExpression? Call(
        this ISqlExpressionFactory sqlExpressionFactory,
        string? template,
        IReadOnlyList<SqlExpression> arguments,
        Type returnType,
        RelationalTypeMapping? typeMapping)
        => template == null
            ? null
            : sqlExpressionFactory.Function(
                Prefix + template, arguments, nullable: true, arguments.Select(_ => true), returnType, typeMapping);

    /// <summary>
    ///     A template that is a search condition (a predicate such as <c>FREETEXT(...)</c>), not a bool value: where a database's
    ///     bools are bit values it is written bare where a condition goes and as a <c>CASE</c> where a value goes, never compared
    ///     with TRUE. The condition is never treated as null. Null when the dialect has no <paramref name="template" />.
    /// </summary>
    public static SqlExpression? Condition(
        this ISqlExpressionFactory sqlExpressionFactory,
        string? template,
        IReadOnlyList<SqlExpression> arguments,
        RelationalTypeMapping? boolTypeMapping)
        => template == null
            ? null
            : sqlExpressionFactory.Function(
                ConditionPrefix + template, arguments, nullable: false, arguments.Select(_ => false), typeof(bool), boolTypeMapping);

    public static bool TryGetTemplate(SqlFunctionExpression function, out string template)
    {
        var prefix = function is not { IsBuiltIn: true, Instance: null } ? null
            : function.Name.StartsWith(Prefix, StringComparison.Ordinal) ? Prefix
            : function.Name.StartsWith(ConditionPrefix, StringComparison.Ordinal) ? ConditionPrefix
            : null;
        template = prefix == null ? "" : function.Name[prefix.Length..];
        return prefix != null;
    }

    /// <summary>Whether <paramref name="expression" /> is a <see cref="Condition" /> template.</summary>
    internal static bool IsCondition(SqlExpression expression)
        => expression is SqlFunctionExpression { IsBuiltIn: true, Instance: null } function
            && function.Name.StartsWith(ConditionPrefix, StringComparison.Ordinal);

    /// <summary>Writes <paramref name="template" />, visiting each <c>{n}</c> argument where it appears.</summary>
    /// <exception cref="InvalidOperationException">The template is malformed or names an argument there isn't.</exception>
    internal static void Write(string template, IReadOnlyList<Expression> arguments, Action<string> append, Action<Expression> visit)
    {
        var text = new StringBuilder();
        foreach (var part in Parse(template, arguments.Count))
        {
            if (part.Argument is { } argument)
            {
                append(text.ToString());
                text.Clear();
                visit(arguments[argument]);
                continue;
            }

            text.Append(part.Text);
        }

        append(text.ToString());
    }

    /// <summary>Why <paramref name="template" /> can't be written with <paramref name="arguments" /> arguments; null when it can.</summary>
    internal static string? Validate(string template, int arguments)
    {
        try
        {
            _ = Parse(template, arguments).Count;
            return null;
        }
        catch (InvalidOperationException e)
        {
            return e.Message;
        }
    }

    private readonly record struct Part(string? Text, int? Argument);

    // Literal text, {{ and }} as braces, and {n} for 0 <= n < arguments; anything else is an error naming the position.
    private static List<Part> Parse(string template, int arguments)
    {
        var parts = new List<Part>();
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            var doubled = (i + 1 < template.Length) && (template[i + 1] == c);
            if ((c is '{' or '}') && doubled)
            {
                parts.Add(new Part(c.ToString(), null));
                i += 2;
                continue;
            }

            if (c == '}')
            {
                throw Malformed(template, i, "a '}' that closes nothing; write '}}' for a brace");
            }

            if (c != '{')
            {
                parts.Add(new Part(c.ToString(), null));
                i++;
                continue;
            }

            var end = template.IndexOf('}', i);
            if (end < 0)
            {
                throw Malformed(template, i, "a '{' that is never closed");
            }

            var index = template.AsSpan(i + 1, end - i - 1);
            if (!int.TryParse(index, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var argument)
                || (argument >= arguments))
            {
                throw Malformed(template, i, $"'{{{index}}}', which is not an argument from {{0}} to {{{arguments - 1}}}");
            }

            parts.Add(new Part(null, argument));
            i = end + 1;
        }

        return parts;
    }

    private static InvalidOperationException Malformed(string template, int position, string problem)
        => new($"The SQL template \"{template}\" has {problem}, at position {position}.");
}

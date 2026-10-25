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

    public static bool TryGetTemplate(SqlFunctionExpression function, out string template)
    {
        var isTemplate = function is { IsBuiltIn: true, Instance: null } && function.Name.StartsWith(Prefix, StringComparison.Ordinal);
        template = isTemplate ? function.Name[Prefix.Length..] : "";
        return isTemplate;
    }

    /// <summary>Writes <paramref name="template" />, visiting each <c>{n}</c> argument where it appears.</summary>
    public static void Write(string template, IReadOnlyList<Expression> arguments, Action<string> append, Action<Expression> visit)
    {
        var text = new StringBuilder();
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if ((c == '{' || c == '}') && i + 1 < template.Length && template[i + 1] == c)
            {
                text.Append(c);
                i++;
            }
            else if (c == '{')
            {
                var end = template.IndexOf('}', i);
                append(text.ToString());
                text.Clear();
                visit(arguments[int.Parse(template.AsSpan(i + 1, end - i - 1))]);
                i = end;
            }
            else
            {
                text.Append(c);
            }
        }

        append(text.ToString());
    }
}

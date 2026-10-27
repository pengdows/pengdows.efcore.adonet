using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     A JSON path as text: <c>$.a[0].b</c>. Each array index that isn't a constant ends a literal run and is returned
///     beside it; member names are delimited by the provider's <see cref="ISqlGenerationHelper.DelimitJsonPathElement" />, to be written as a computed value: <c>Literals[0] + Indexes[0] + Literals[1] + ...</c>.
/// </summary>
internal sealed record JsonPathText(IReadOnlyList<string> Literals, IReadOnlyList<SqlExpression> Indexes)
{
    public static JsonPathText Of(IReadOnlyList<PathSegment> path, ISqlGenerationHelper helper)
    {
        var literals = new List<string>();
        var indexes = new List<SqlExpression>();
        var text = new System.Text.StringBuilder("$");
        foreach (var segment in path)
        {
            if (segment.PropertyName is { } name)
            {
                text.Append('.').Append(helper.DelimitJsonPathElement(name));
            }
            else if (segment.ArrayIndex is SqlConstantExpression { Value: var index })
            {
                text.Append('[').Append(Convert.ToString(index, System.Globalization.CultureInfo.InvariantCulture)).Append(']');
            }
            else
            {
                literals.Add(text.Append('[').ToString());
                indexes.Add(segment.ArrayIndex!);
                text.Clear().Append(']');
            }
        }

        literals.Add(text.ToString());
        return new JsonPathText(literals, indexes);
    }
}

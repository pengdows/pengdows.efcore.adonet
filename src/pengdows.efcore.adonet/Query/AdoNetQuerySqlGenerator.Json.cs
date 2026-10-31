using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using pengdows.efcore.adonet.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
namespace pengdows.efcore.adonet.Query;

public partial class AdoNetQuerySqlGenerator
{
    // A value inside a JSON column, with the dialect's JSON functions: a sub-document (structural type or primitive
    // collection) as JSON, a string as text, any other type returned as its store type. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerQuerySqlGenerator.cs
    protected override Expression VisitJsonScalar(JsonScalarExpression jsonScalarExpression)
    {
        if (jsonScalarExpression.Path.Count == 0)
        {
            Visit(jsonScalarExpression.Json);
            return jsonScalarExpression;
        }

        var json = dialect.Json
            ?? throw new InvalidOperationException("This query reads a value inside a JSON column, which the dialect has no functions for.");
        var typeMapping = jsonScalarExpression.TypeMapping;

        // A DateTimeOffset is JSON text with its offset, which some databases' conversions ignore: read as text, then converted.
        if ((json.DateTimeOffsetFromText is { } fromText)
            && ((Nullable.GetUnderlyingType(jsonScalarExpression.Type) ?? jsonScalarExpression.Type) == typeof(DateTimeOffset)))
        {
            WriteTemplate(
                fromText,
                () => WriteTemplate(json.Value, () => Visit(jsonScalarExpression.Json), () => WriteJsonPath(json, jsonScalarExpression.Path)));
            return jsonScalarExpression;
        }
        var template = (Nullable.GetUnderlyingType(jsonScalarExpression.Type) ?? jsonScalarExpression.Type) == typeof(byte[]) && json.BinaryValue != null ? json.BinaryValue
            : typeMapping is Storage.AdoNetJsonTypeMapping || typeMapping?.ElementTypeMapping != null ? json.Query
            : typeMapping is not null
                && (Nullable.GetUnderlyingType(typeMapping.ClrType) ?? typeMapping.ClrType) == typeof(bool)
                && typeMapping.Converter?.ProviderClrType != typeof(string)
                && (json.BooleanValue != null) ? json.BooleanValue
            : typeMapping is null or StringTypeMapping ? json.Value
            : json.TypedValue;

        WriteTemplate(
            template,
            () => Visit(jsonScalarExpression.Json),
            () => WriteJsonPath(json, jsonScalarExpression.Path),
            () => Sql.Append(typeMappings.CastTarget(typeMapping!.StoreType)));
        return jsonScalarExpression;
    }

    // A JSON scalar is written as a function call (the dialect's templates), so it never needs parentheses.

    // '$.a[0].b' when every array index is a constant; otherwise the constant runs joined to the computed indexes, each cast
    // to text, with the dialect's concatenation.
    private void WriteJsonPath(JsonSyntax json, IReadOnlyList<PathSegment> path)
    {
        var text = JsonPathText.Of(path, Dependencies.SqlGenerationHelper);
        var parts = new List<Action>();
        for (var i = 0; i < text.Indexes.Count; i++)
        {
            var literal = text.Literals[i];
            var computed = text.Indexes[i];
            parts.Add(() => Sql.Append(dialect.StringLiteral(literal)));
            parts.Add(() => WriteCast(computed, typeMappings.GetStoreType(typeof(string))!));
        }

        var rest = text.Literals[^1];
        parts.Add(() => Sql.Append(dialect.StringLiteral(rest)));
        WriteConcatenation(json.Concatenate, parts, 0);
    }

    // A JSON column compared with JSON text: a bare string is a JSON string scalar to some databases, which equals no document,
    // so the text is turned into a JSON value first. A column that stores its document as plain text compares as text.
    private bool TryWriteJsonEquality(SqlBinaryExpression expression)
    {
        if ((expression.OperatorType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            || (dialect.Json is not { StoresDocumentAsText: false } json)
            || (FindJsonTextOperand(expression, json) is not { } text))
        {
            return false;
        }

        // A database whose JSON is text underneath compares documents with its own function, as JSON, not as text.
        if (json.Equality != null)
        {
            var notEqual = expression.OperatorType == ExpressionType.NotEqual;
            Sql.Append(notEqual ? "NOT (" : "");
            SqlTemplate.Write(json.Equality, [expression.Left, expression.Right], s => Sql.Append(s), e => Visit(e));
            Sql.Append(notEqual ? ")" : "");
            return true;
        }

        WriteJsonComparisonOperand(expression.Left, text, json);
        Sql.Append(expression.OperatorType == ExpressionType.Equal ? " = " : " <> ");
        WriteJsonComparisonOperand(expression.Right, text, json);
        return true;
    }

    private void WriteJsonComparisonOperand(SqlExpression operand, SqlExpression text, JsonSyntax json)
    {
        if (!ReferenceEquals(operand, text))
        {
            Visit(operand);
            return;
        }

        SqlTemplate.Write(json.FromText, [operand], s => Sql.Append(s), e => Visit(e));
    }

    private static bool IsJsonDocument(SqlExpression operand, JsonSyntax json)
        => (operand is ColumnExpression)
           && string.Equals(operand.TypeMapping?.StoreType, json.StoreType, StringComparison.OrdinalIgnoreCase);

    private static SqlExpression? FindJsonTextOperand(SqlBinaryExpression expression, JsonSyntax json)
    {
        if (IsJsonDocument(expression.Left, json) && IsConstantOrParameter(expression.Right))
        {
            return expression.Right;
        }

        if (IsJsonDocument(expression.Right, json) && IsConstantOrParameter(expression.Left))
        {
            return expression.Left;
        }

        return null;
    }
}

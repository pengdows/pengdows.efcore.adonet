using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using pengdows.efcore.adonet.Query;
namespace pengdows.efcore.adonet;

/// <summary>
///     JSON columns (owned and complex types mapped with <c>ToJson</c>): the column's type, and how a value inside a document
///     is read and patched. <c>{0}</c> is the JSON column or expression, <c>{1}</c> the SQL/JSON path literal (<c>'$.a[0].b'</c>),
///     <c>{2}</c> the store type a value is returned as, or the new value. Each reading template is a function call, so it is
///     never parenthesized as an operand.
/// </summary>
/// <param name="storeType">The JSON column's type.</param>
/// <param name="value">A string value inside the document.</param>
/// <param name="query">A sub-document (an object or array), as JSON.</param>
/// <param name="typedValue">A value inside the document returned as the store type <c>{2}</c>.</param>
/// <param name="booleanValue">A boolean value inside the document, where a typed value can't express it.</param>
/// <param name="binaryValue">A base64-encoded binary value inside the document, decoded to bytes.</param>
/// <param name="concatenate">Joins two strings, to build a path whose array index is computed: <c>{0} || {1}</c>.</param>
/// <param name="set">Replaces the value at the path <c>{1}</c> in <c>{0}</c> with <c>{2}</c>.</param>
/// <param name="fromText">Turns JSON text (a sub-document, or <c>true</c>/<c>false</c>) into a JSON value.</param>
/// <param name="setBooleanValue">Converts a boolean parameter to the database's JSON boolean value at a partial-update path.</param>
public sealed class JsonSyntax(
    string storeType,
    string value,
    string query,
    string typedValue,
    string? booleanValue,
    string? binaryValue,
    string concatenate,
    string? set,
    string fromText,
    string? setBooleanValue = null)
{
    /// <summary>
    ///     SQL:2016's JSON_VALUE and JSON_QUERY, SQL:2023's JSON type and JSON() constructor, and JSON_SET, which ISO lacks and
    ///     several databases share.
    /// </summary>
    public static JsonSyntax Iso { get; } = new(
        "JSON",
        "JSON_VALUE({0}, {1})",
        "JSON_QUERY({0}, {1})",
        "JSON_VALUE({0}, {1} RETURNING {2})",
        booleanValue: null,
        binaryValue: null,
        "{0} || {1}",
        "JSON_SET({0}, {1}, {2})",
        "JSON({0})");

    /// <summary>The database's JSON column type.</summary>
    public string StoreType { get; } = storeType;
    /// <summary>A JSON value at a path, as a SQL value expression.</summary>
    public string Value { get; } = value;
    /// <summary>A JSON value at a path, as a JSON expression.</summary>
    public string Query { get; } = query;
    /// <summary>A JSON value at a path, cast to a SQL type.</summary>
    public string TypedValue { get; } = typedValue;
    /// <summary>A boolean inside a JSON document, where a typed value can't express it.</summary>
    public string? BooleanValue { get; } = booleanValue;
    /// <summary>A base64-encoded binary value inside a JSON document, decoded to bytes.</summary>
    public string? BinaryValue { get; } = binaryValue;
    /// <summary>Joins two strings into a JSON path.</summary>
    public string Concatenate { get; } = concatenate;
    /// <summary>Replaces the value at a path in a JSON document.</summary>
    public string? Set { get; } = set;
    /// <summary>Turns JSON text into a JSON value.</summary>
    public string FromText { get; } = fromText;

    /// <summary>
    ///     Converts a boolean parameter for <see cref="Set" />; <c>{0}</c> is the parameter expression. Null when the normal
    ///     JSON text conversion is sufficient.
    /// </summary>
    public string? SetBooleanValue { get; } = setBooleanValue;

    /// <summary>Whether the database stores JSON in a native binary document type.</summary>
    public bool UsesBinaryDocument { get; init; }

    /// <summary>
    ///     Sets the driver's type for a parameter holding JSON, where the driver needs one for a native JSON document type. The
    ///     provider owns the driver's API; the shared code only decides which parameters are JSON.
    /// </summary>
    public Action<DbParameter>? ConfigureParameter { get; init; }

    /// <summary>
    ///     A SQL value <c>{0}</c> as a JSON scalar, for <see cref="Set" />; null where the set function takes a SQL value as it is.
    ///     A value JSON holds as a string (a date, a uuid, bytes as base64) is converted to text first.
    /// </summary>
    public string? ScalarValue { get; init; }

    /// <summary>JSON's null, for <see cref="Set" /> to store a null property; null where it stores SQL NULL as JSON null.</summary>
    public string? NullValue { get; init; }

    /// <summary>
    ///     Whether a JSON column is plain text, so it compares with a string as text and the string takes no
    ///     <see cref="FromText" /> conversion.
    /// </summary>
    public bool StoresDocumentAsText { get; init; }

    /// <summary>
    ///     Whether the JSON documents <c>{0}</c> and <c>{1}</c> are equal, for a JSON column compared with JSON text: null to
    ///     compare with <c>=</c> after <see cref="FromText" />. For a database whose JSON is text underneath, where <c>=</c>
    ///     compares spellings, not documents.
    /// </summary>
    public string? Equality { get; init; }

    /// <summary>
    ///     The elements of the JSON array <c>{0}</c> as a table, for querying a primitive collection: a column named <c>{1}</c>
    ///     that orders the elements, and one named <c>{2}</c> holding each element as the store type <c>{3}</c>. The table's alias
    ///     follows it. Null where the dialect can't; a collection is then queried only as a whole.
    /// </summary>
    public string? ArrayElements { get; init; }

    /// <summary>
    ///     The <c>{3}</c> of <see cref="ArrayElements" /> for text elements, from the element's store type <c>{0}</c>; null to use
    ///     the store type as it is.
    /// </summary>
    public string? ArrayElementsTextType { get; init; }

    /// <summary>
    ///     Decodes a binary element <c>{0}</c> of <see cref="ArrayElements" />, which JSON holds as base64 text; null where binary
    ///     elements can't be read. The element column is then declared as text.
    /// </summary>
    public string? ArrayElementsBinaryValue { get; init; }

    /// <summary>
    ///     A DateTimeOffset from its JSON text <c>{0}</c> (ISO 8601 with its offset), for a database whose conversion of that text
    ///     ignores the offset; null to read it as any other typed value. Applies to a value read out of a document and to an
    ///     element of <see cref="ArrayElements" />, which is then declared as text.
    /// </summary>
    public string? DateTimeOffsetFromText { get; init; }

    /// <summary>An optimizer hint written after <c>SELECT</c> in a query that reads <see cref="ArrayElements" />; null for none.</summary>
    public string? ArrayElementsHint { get; init; }
}

using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     A JSON column holding owned or complex types (ToJson). The document is read as a string, which EF parses through a
///     stream of its UTF-8 bytes; a literal is a string literal in the dialect's quoting.
/// </summary>
/// <remarks>
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Storage/Internal/SqliteJsonTypeMapping.cs
/// </remarks>
internal sealed class AdoNetJsonTypeMapping : JsonTypeMapping
{
    private static readonly MethodInfo GetString = typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.GetString), [typeof(int)])!;
    private static readonly PropertyInfo Utf8 = typeof(Encoding).GetProperty(nameof(Encoding.UTF8))!;
    private static readonly MethodInfo GetBytes = typeof(Encoding).GetMethod(nameof(Encoding.GetBytes), [typeof(string)])!;
    private static readonly ConstructorInfo NewMemoryStream = typeof(MemoryStream).GetConstructor([typeof(byte[])])!;

    private readonly ISqlDialect _sql;

    public AdoNetJsonTypeMapping(string storeType, ISqlDialect sql)
        // A native binary JSON column cannot accept a parameter explicitly typed as text. Object leaves the
        // provider parameter type open so the driver can send the JSON document with its native handler;
        // other dialects retain the portable string binding.
        : base(storeType, typeof(JsonTypePlaceholder),
            sql.Json?.UsesBinaryDocument == true
                ? System.Data.DbType.Object
                : System.Data.DbType.String)
        => _sql = sql;

    private AdoNetJsonTypeMapping(RelationalTypeMappingParameters parameters, ISqlDialect sql)
        : base(parameters)
        => _sql = sql;

    public override MethodInfo GetDataReaderMethod()
        => GetString;

    public override Expression CustomizeDataReaderExpression(Expression expression)
        => Expression.New(NewMemoryStream, Expression.Call(Expression.Property(null, Utf8), GetBytes, expression));

    protected override string GenerateNonNullSqlLiteral(object value)
        => _sql.StringLiteral((string)value);

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);

        // DbType has no JSON member. A driver may expose the required provider-specific type through
        // a public property; use reflection without taking a driver dependency
        // in the shared provider assembly.
        if (_sql.Json?.UsesBinaryDocument != true)
        {
            return;
        }

        // JSON partial updates can carry a native provider value (interval, timestamp, UUID, ...).
        // Marking those parameters as binary JSON makes the driver parse their display text before SQL can
        // convert them into JSON strings. Force binary JSON only for already encoded JSON documents.
        if (parameter.Value is not JsonDocument && parameter.Value is not JsonElement
            && !(parameter.Value is string text && IsJson(text)))
        {
            return;
        }

        _sql.Json?.ConfigureParameter?.Invoke(parameter);
    }

    private static bool IsJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is not JsonValueKind.Undefined;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetJsonTypeMapping(parameters, _sql);
}

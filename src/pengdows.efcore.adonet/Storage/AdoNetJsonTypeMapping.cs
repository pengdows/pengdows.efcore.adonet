using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
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
        : base(storeType, typeof(JsonTypePlaceholder), System.Data.DbType.String)
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

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetJsonTypeMapping(parameters, _sql);
}

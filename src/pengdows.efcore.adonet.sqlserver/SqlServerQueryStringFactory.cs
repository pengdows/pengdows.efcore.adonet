using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     <c>ToQueryString</c> as runnable T-SQL: each parameter is a <c>DECLARE</c> of the store type this dialect maps its value
///     and facets to, set to the literal its type mapping writes.
/// </summary>
/// <remarks>
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerQueryStringFactory.cs
/// </remarks>
internal class SqlServerQueryStringFactory(IRelationalTypeMappingSource typeMappingSource, IAdoNetDialect dialect)
    : IRelationalQueryStringFactory
{
    public virtual string Create(DbCommand command)
    {
        if (command.Parameters.Count == 0)
        {
            return command.CommandText;
        }

        var sql = new StringBuilder();
        foreach (DbParameter parameter in command.Parameters)
        {
            var storeType = StoreType(parameter);
            sql.Append("DECLARE ")
                .Append(parameter.ParameterName)
                .Append(' ')
                .Append(storeType)
                .Append(" = ")
                .Append(Literal(parameter, storeType))
                .AppendLine(";");
        }

        return sql.AppendLine().Append(command.CommandText).ToString();
    }

    private string StoreType(DbParameter parameter)
    {
        if (ValueType(parameter) is not { } type)
        {
            return "sql_variant";
        }

        var facets = new StoreTypeFacets(
            Size: Positive(parameter.Size),
            Precision: Positive(parameter.Precision),
            Scale: Positive(parameter.Scale),
            FixedLength: parameter.DbType is DbType.StringFixedLength or DbType.AnsiStringFixedLength,
            Unicode: parameter.DbType is not (DbType.AnsiString or DbType.AnsiStringFixedLength));

        return dialect.TypeMappings.GetStoreType(type, facets) ?? dialect.TypeMappings.GetStoreType(type) ?? "sql_variant";
    }

    // A value without an SQL literal is refused rather than written as text that could be read as SQL.
    private string Literal(DbParameter parameter, string storeType)
    {
        if (parameter.Value is null or DBNull)
        {
            return "NULL";
        }

        var mapping = typeMappingSource.FindMapping(parameter.Value.GetType(), storeType)
            ?? throw new NotSupportedException(
                $"Parameter '{parameter.ParameterName}' holds a {parameter.Value.GetType().Name}, which has no SQL literal in {dialect.Name}.");
        return mapping.GenerateSqlLiteral(parameter.Value);
    }

    private static Type? ValueType(DbParameter parameter)
        => parameter.Value is { } value and not DBNull
            ? value.GetType()
            : parameter.DbType switch
            {
                DbType.String or DbType.StringFixedLength or DbType.AnsiString or DbType.AnsiStringFixedLength => typeof(string),
                DbType.Boolean => typeof(bool),
                DbType.Byte => typeof(byte),
                DbType.Int16 => typeof(short),
                DbType.Int32 => typeof(int),
                DbType.Int64 => typeof(long),
                DbType.Single => typeof(float),
                DbType.Double => typeof(double),
                DbType.Decimal or DbType.Currency => typeof(decimal),
                DbType.Date => typeof(DateOnly),
                DbType.Time => typeof(TimeOnly),
                DbType.DateTime or DbType.DateTime2 => typeof(DateTime),
                DbType.DateTimeOffset => typeof(DateTimeOffset),
                DbType.Guid => typeof(Guid),
                DbType.Binary => typeof(byte[]),
                _ => null
            };

    private static int? Positive(int value)
        => value > 0 ? value : null;
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     A PostgreSQL type Npgsql reads and binds as one of its own CLR types (tsvector, inet, a range, ...): bound with its
///     <see cref="NpgsqlDbType" />, written as a literal by casting the value's text form, which Npgsql's types print as PostgreSQL
///     reads them. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/NpgsqlTypeMappingSource.cs
/// </summary>
internal sealed class PostgreSqlProviderTypeMapping : RelationalTypeMapping
{
    private readonly NpgsqlDbType _npgsqlDbType;
    private readonly Func<object, string> _text;

    public PostgreSqlProviderTypeMapping(Type clrType, string storeType, NpgsqlDbType npgsqlDbType, Func<object, string>? text = null)
        : this(
            new RelationalTypeMappingParameters(new CoreTypeMappingParameters(clrType), storeType),
            npgsqlDbType,
            text ?? (value => value.ToString()!))
    {
    }

    private PostgreSqlProviderTypeMapping(RelationalTypeMappingParameters parameters, NpgsqlDbType npgsqlDbType, Func<object, string> text)
        : base(parameters)
    {
        _npgsqlDbType = npgsqlDbType;
        _text = text;
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlProviderTypeMapping(parameters, _npgsqlDbType, _text);

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if (parameter is NpgsqlParameter npgsql)
        {
            npgsql.NpgsqlDbType = _npgsqlDbType;
        }
    }

    protected override string GenerateNonNullSqlLiteral(object value)
        => "'" + _text(value).Replace("'", "''", StringComparison.Ordinal) + "'::" + StoreType;
}

using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>Binds as <see cref="DbType.String" />; constants are written by the dialect's <see cref="ISqlDialect.StringLiteral" />.</summary>
public class AdoNetStringTypeMapping : StringTypeMapping
{
    private readonly ISqlDialect _sql;

    public AdoNetStringTypeMapping(string storeType, ISqlDialect sql)
        : base(storeType, System.Data.DbType.String, unicode: true)
        => _sql = sql;

    protected AdoNetStringTypeMapping(RelationalTypeMappingParameters parameters, ISqlDialect sql)
        : base(parameters)
        => _sql = sql;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetStringTypeMapping(parameters, _sql);

    protected override string GenerateNonNullSqlLiteral(object value)
        => _sql.StringLiteral((string)value);
}

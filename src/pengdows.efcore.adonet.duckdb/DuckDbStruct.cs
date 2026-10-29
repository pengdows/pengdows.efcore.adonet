using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.duckdb;

/// <summary>Maps a manually declared DuckDB STRUCT column to its CLR value object.</summary>
internal sealed class DuckDbStructTypeMappingSourcePlugin : IRelationalTypeMappingSourcePlugin
{
    public RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        if ((mappingInfo.ClrType is not { IsClass: true } clrType)
            || mappingInfo.StoreTypeName is not { } storeType
            || !storeType.TrimStart().StartsWith("STRUCT(", StringComparison.OrdinalIgnoreCase)
            || clrType == typeof(string))
        {
            return null;
        }

        return new DuckDbStructTypeMapping(clrType, storeType);
    }
}

internal sealed class DuckDbStructTypeMapping : RelationalTypeMapping
{
    public DuckDbStructTypeMapping(Type clrType, string storeType)
        : this(new RelationalTypeMappingParameters(new CoreTypeMappingParameters(clrType), storeType))
    {
    }

    private DuckDbStructTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new DuckDbStructTypeMapping(parameters);

    protected override string GenerateNonNullSqlLiteral(object value)
        => throw new NotSupportedException("DuckDB STRUCT literals require provider-specific field syntax.");
}

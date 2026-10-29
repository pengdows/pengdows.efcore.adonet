using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore.Design;
using pengdows.efcore.adonet.Design;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.duckdb.DuckDbDesignTimeServices")]

namespace pengdows.efcore.adonet.duckdb;

/// <summary>The DuckDB provider's design-time services for migrations and reverse engineering, through DuckDB.NET.</summary>
public sealed class DuckDbDesignTimeServices() : AdoNetDesignTimeServices(DuckDbDialect.Instance, DuckDBClientFactory.Instance)
{
    protected override System.Reflection.MethodInfo UseProvider
        => AdoNetProviderCodeGenerator.UseMethod(typeof(DuckDbDbContextOptionsBuilderExtensions), nameof(DuckDbDbContextOptionsBuilderExtensions.UseAdoNetDuckDb));
}

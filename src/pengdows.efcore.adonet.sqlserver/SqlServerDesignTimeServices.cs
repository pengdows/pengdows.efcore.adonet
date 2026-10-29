using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Design;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.sqlserver.SqlServerDesignTimeServices")]

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>The SQL Server provider's design-time services.</summary>
public sealed class SqlServerDesignTimeServices() : AdoNetDesignTimeServices(SqlServerDialect.Instance, Microsoft.Data.SqlClient.SqlClientFactory.Instance)
{
    protected override System.Reflection.MethodInfo UseProvider
        => AdoNetProviderCodeGenerator.UseMethod(typeof(SqlServerDbContextOptionsBuilderExtensions), nameof(SqlServerDbContextOptionsBuilderExtensions.UseAdoNetSqlServer));
}

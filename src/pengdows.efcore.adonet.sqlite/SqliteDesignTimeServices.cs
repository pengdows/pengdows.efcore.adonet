using Microsoft.EntityFrameworkCore.Design;
using pengdows.efcore.adonet.Design;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.sqlite.SqliteDesignTimeServices")]

namespace pengdows.efcore.adonet.sqlite;

/// <summary>The SQLite provider's design-time services for migrations and reverse engineering.</summary>
public sealed class SqliteDesignTimeServices() : AdoNetDesignTimeServices(SqliteDialect.Instance, Microsoft.Data.Sqlite.SqliteFactory.Instance)
{
    protected override System.Reflection.MethodInfo UseProvider
        => AdoNetProviderCodeGenerator.UseMethod(typeof(SqliteDbContextOptionsBuilderExtensions), nameof(SqliteDbContextOptionsBuilderExtensions.UseAdoNetSqlite));
}

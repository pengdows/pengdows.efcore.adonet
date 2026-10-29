using Microsoft.EntityFrameworkCore.Design;
using pengdows.efcore.adonet.Design;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.postgresql.PostgreSqlDesignTimeServices")]

namespace pengdows.efcore.adonet.postgresql;

/// <summary>The PostgreSQL provider's design-time services for migrations and reverse engineering, through Npgsql.</summary>
public sealed class PostgreSqlDesignTimeServices() : AdoNetDesignTimeServices(PostgreSqlDialect.Instance, Npgsql.NpgsqlFactory.Instance)
{
    protected override System.Reflection.MethodInfo UseProvider
        => AdoNetProviderCodeGenerator.UseMethod(typeof(PostgreSqlDbContextOptionsBuilderExtensions), nameof(PostgreSqlDbContextOptionsBuilderExtensions.UseAdoNetPostgreSql));
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Testing;
using Microsoft.Data.SqlClient;

namespace pengdows.efcore.adonet.sqlserver.Tests;

public sealed class SqlServerEntryPoints : AdoNetEntryPoints
{
    public override IAdoNetDialect Dialect => SqlServerDialect.Instance;

    public override DbProviderFactory Factory => SqlClientFactory.Instance;

    public override string ConnectionString => "Server=localhost";

    public override DbContextOptionsBuilder<BlogContext> UseConnectionString(DbContextOptionsBuilder<BlogContext> builder, string connectionString)
        => builder.UseAdoNetSqlServer(connectionString);

    public override DbContextOptionsBuilder UseConnectionString(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseAdoNetSqlServer(connectionString);

    public override DbContextOptionsBuilder<BlogContext> UseConnection(
        DbContextOptionsBuilder<BlogContext> builder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetSqlServer(connection, contextOwnsConnection, configure);

    public override DbContextOptionsBuilder<BlogContext> UseDataSource(DbContextOptionsBuilder<BlogContext> builder, DbDataSource dataSource)
        => builder.UseAdoNetSqlServer(dataSource);

    public override DbContextOptionsBuilder<BlogContext> UseNoConnection(DbContextOptionsBuilder<BlogContext> builder)
        => builder.UseAdoNetSqlServer();
}

public class SqlServerEntryPointTests : EntryPointConformanceTests<SqlServerEntryPoints>;

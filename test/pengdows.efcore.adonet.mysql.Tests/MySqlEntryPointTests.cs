using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Testing;
using MySqlConnector;

namespace pengdows.efcore.adonet.mysql.Tests;

public sealed class MySqlEntryPoints : AdoNetEntryPoints
{
    public override IAdoNetDialect Dialect => MySqlDialect.Instance;

    public override DbProviderFactory Factory => MySqlConnectorFactory.Instance;

    public override string ConnectionString => "Server=localhost";

    public override DbContextOptionsBuilder<BlogContext> UseConnectionString(DbContextOptionsBuilder<BlogContext> builder, string connectionString)
        => builder.UseAdoNetMySql(connectionString);

    public override DbContextOptionsBuilder UseConnectionString(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseAdoNetMySql(connectionString);

    public override DbContextOptionsBuilder<BlogContext> UseConnection(
        DbContextOptionsBuilder<BlogContext> builder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetMySql(connection, contextOwnsConnection, configure);

    public override DbContextOptionsBuilder<BlogContext> UseDataSource(DbContextOptionsBuilder<BlogContext> builder, DbDataSource dataSource)
        => builder.UseAdoNetMySql(dataSource);

    public override DbContextOptionsBuilder<BlogContext> UseNoConnection(DbContextOptionsBuilder<BlogContext> builder)
        => builder.UseAdoNetMySql();
}

public class MySqlEntryPointTests : EntryPointConformanceTests<MySqlEntryPoints>;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Testing;
using Npgsql;

namespace pengdows.efcore.adonet.postgresql.Tests;

public sealed class PostgreSqlEntryPoints : AdoNetEntryPoints
{
    public override IAdoNetDialect Dialect => PostgreSqlDialect.Instance;

    public override DbProviderFactory Factory => NpgsqlFactory.Instance;

    public override string ConnectionString => "Host=localhost";

    public override DbContextOptionsBuilder<BlogContext> UseConnectionString(DbContextOptionsBuilder<BlogContext> builder, string connectionString)
        => builder.UseAdoNetPostgreSql(connectionString);

    public override DbContextOptionsBuilder UseConnectionString(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseAdoNetPostgreSql(connectionString);

    public override DbContextOptionsBuilder<BlogContext> UseConnection(
        DbContextOptionsBuilder<BlogContext> builder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetPostgreSql(connection, contextOwnsConnection, configure);

    public override DbContextOptionsBuilder<BlogContext> UseDataSource(DbContextOptionsBuilder<BlogContext> builder, DbDataSource dataSource)
        => builder.UseAdoNetPostgreSql(dataSource);

    public override DbContextOptionsBuilder<BlogContext> UseNoConnection(DbContextOptionsBuilder<BlogContext> builder)
        => builder.UseAdoNetPostgreSql();
}

public class PostgreSqlEntryPointTests : EntryPointConformanceTests<PostgreSqlEntryPoints>;

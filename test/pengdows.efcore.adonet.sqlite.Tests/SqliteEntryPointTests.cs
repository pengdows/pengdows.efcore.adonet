using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Testing;
using Microsoft.Data.Sqlite;

namespace pengdows.efcore.adonet.sqlite.Tests;

public sealed class SqliteEntryPoints : AdoNetEntryPoints
{
    public override IAdoNetDialect Dialect => SqliteDialect.Instance;

    public override DbProviderFactory Factory => SqliteFactory.Instance;

    public override string ConnectionString => "Data Source=:memory:";

    public override DbContextOptionsBuilder<BlogContext> UseConnectionString(DbContextOptionsBuilder<BlogContext> builder, string connectionString)
        => builder.UseAdoNetSqlite(connectionString);

    public override DbContextOptionsBuilder UseConnectionString(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseAdoNetSqlite(connectionString);

    public override DbContextOptionsBuilder<BlogContext> UseConnection(
        DbContextOptionsBuilder<BlogContext> builder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetSqlite(connection, contextOwnsConnection, configure);

    public override DbContextOptionsBuilder<BlogContext> UseDataSource(DbContextOptionsBuilder<BlogContext> builder, DbDataSource dataSource)
        => builder.UseAdoNetSqlite(dataSource);

    public override DbContextOptionsBuilder<BlogContext> UseNoConnection(DbContextOptionsBuilder<BlogContext> builder)
        => builder.UseAdoNetSqlite();
}

public class SqliteEntryPointTests : EntryPointConformanceTests<SqliteEntryPoints>;

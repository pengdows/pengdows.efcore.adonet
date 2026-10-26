using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Testing;
using DuckDB.NET.Data;

namespace pengdows.efcore.adonet.duckdb.Tests;

public sealed class DuckDbEntryPoints : AdoNetEntryPoints
{
    public override IAdoNetDialect Dialect => DuckDbDialect.Instance;

    public override DbProviderFactory Factory => DuckDBClientFactory.Instance;

    public override string ConnectionString => "Data Source=:memory:";

    public override DbContextOptionsBuilder<BlogContext> UseConnectionString(DbContextOptionsBuilder<BlogContext> builder, string connectionString)
        => builder.UseAdoNetDuckDb(connectionString);

    public override DbContextOptionsBuilder UseConnectionString(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseAdoNetDuckDb(connectionString);

    public override DbContextOptionsBuilder<BlogContext> UseConnection(
        DbContextOptionsBuilder<BlogContext> builder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetDuckDb(connection, contextOwnsConnection, configure);

    public override DbContextOptionsBuilder<BlogContext> UseDataSource(DbContextOptionsBuilder<BlogContext> builder, DbDataSource dataSource)
        => builder.UseAdoNetDuckDb(dataSource);

    public override DbContextOptionsBuilder<BlogContext> UseNoConnection(DbContextOptionsBuilder<BlogContext> builder)
        => builder.UseAdoNetDuckDb();
}

public class DuckDbEntryPointTests : EntryPointConformanceTests<DuckDbEntryPoints>;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Testing;
using pengdows.flatfile;

namespace pengdows.efcore.adonet.flatfile.Tests;

public sealed class FlatFileEntryPoints : AdoNetEntryPoints
{
    public override IAdoNetDialect Dialect => FlatFileDialect.Instance;

    public override DbProviderFactory Factory => FlatFileProviderFactory.Instance;

    public override string ConnectionString => "path=.";

    public override DbContextOptionsBuilder<BlogContext> UseConnectionString(DbContextOptionsBuilder<BlogContext> builder, string connectionString)
        => builder.UseAdoNetFlatFile(connectionString);

    public override DbContextOptionsBuilder UseConnectionString(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseAdoNetFlatFile(connectionString);

    public override DbContextOptionsBuilder<BlogContext> UseConnection(
        DbContextOptionsBuilder<BlogContext> builder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetFlatFile(connection, contextOwnsConnection, configure);

    public override DbContextOptionsBuilder<BlogContext> UseDataSource(DbContextOptionsBuilder<BlogContext> builder, DbDataSource dataSource)
        => builder.UseAdoNetFlatFile(dataSource);

    public override DbContextOptionsBuilder<BlogContext> UseNoConnection(DbContextOptionsBuilder<BlogContext> builder)
        => builder.UseAdoNetFlatFile();
}

public class FlatFileEntryPointTests : EntryPointConformanceTests<FlatFileEntryPoints>;

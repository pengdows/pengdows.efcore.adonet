using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.flatfile;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.flatfile;

/// <summary>
///     pengdows.flatfile through FlatFileProviderFactory, with the FlatFileDialect: from a connection string, a connection, a data source, or a connection string
///     or connection set later through <c>Database</c>. <c>adoNetOptionsAction</c> runs after the dialect is set.
/// </summary>
public static class FlatFileDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseAdoNetFlatFile(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(FlatFileProviderFactory.Instance, connectionString, FlatFileDialect.Instance, adoNetOptionsAction);

    /// <summary>Over <paramref name="connection" />, which the caller owns unless <paramref name="contextOwnsConnection" />.</summary>
    public static DbContextOptionsBuilder UseAdoNetFlatFile(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(connection, contextOwnsConnection, With(adoNetOptionsAction));

    /// <summary>Connections from <paramref name="dataSource" />, which the caller owns.</summary>
    public static DbContextOptionsBuilder UseAdoNetFlatFile(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(dataSource, FlatFileDialect.Instance, adoNetOptionsAction);

    /// <summary>The connection string, or connection, is set later through <c>Database</c>.</summary>
    public static DbContextOptionsBuilder UseAdoNetFlatFile(
        this DbContextOptionsBuilder optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(FlatFileProviderFactory.Instance, With(adoNetOptionsAction));

    public static DbContextOptionsBuilder<TContext> UseAdoNetFlatFile<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetFlatFile(connectionString, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetFlatFile<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetFlatFile(
            connection, contextOwnsConnection, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetFlatFile<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetFlatFile(dataSource, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetFlatFile<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetFlatFile(adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(FlatFileProviderFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<FlatFileProviderFactory>(FlatFileDialect.Instance);

    private static Action<AdoNetDbContextOptionsBuilder> With(Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction)
        => o =>
        {
            o.UseDialect(FlatFileDialect.Instance);
            adoNetOptionsAction?.Invoke(o);
        };
}

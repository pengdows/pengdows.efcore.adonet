using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using DuckDB.NET.Data;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.duckdb;

/// <summary>
///     DuckDB through DuckDBClientFactory, with the DuckDbDialect: from a connection string, a connection, a data source, or a connection string
///     or connection set later through <c>Database</c>. <c>adoNetOptionsAction</c> runs after the dialect is set.
/// </summary>
public static class DuckDbDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseAdoNetDuckDb(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            DuckDBClientFactory.Instance,
            connectionString,
            DuckDbDialect.Instance,
            AdoNetProviderOptions.Configure(DuckDbDialect.Instance, adoNetOptionsAction));

    /// <summary>
    ///     The same DuckDbDialect dialect over any driver: <paramref name="providerFactory" /> supplies the connections, so a
    ///     driver is swapped at this entry point (the default driver is the one the other overloads use).
    /// </summary>
    public static DbContextOptionsBuilder UseAdoNetDuckDb(
        this DbContextOptionsBuilder optionsBuilder,
        DbProviderFactory providerFactory,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            providerFactory,
            connectionString,
            DuckDbDialect.Instance,
            AdoNetProviderOptions.Configure(DuckDbDialect.Instance, adoNetOptionsAction));

    /// <summary>Over <paramref name="connection" />, which the caller owns unless <paramref name="contextOwnsConnection" />.</summary>
    public static DbContextOptionsBuilder UseAdoNetDuckDb(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(connection, contextOwnsConnection, AdoNetProviderOptions.Configure(DuckDbDialect.Instance, adoNetOptionsAction));

    /// <summary>Connections from <paramref name="dataSource" />, which the caller owns.</summary>
    public static DbContextOptionsBuilder UseAdoNetDuckDb(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            dataSource,
            DuckDbDialect.Instance,
            AdoNetProviderOptions.Configure(DuckDbDialect.Instance, adoNetOptionsAction));

    /// <summary>The connection string, or connection, is set later through <c>Database</c>.</summary>
    public static DbContextOptionsBuilder UseAdoNetDuckDb(
        this DbContextOptionsBuilder optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(DuckDBClientFactory.Instance, AdoNetProviderOptions.Configure(DuckDbDialect.Instance, adoNetOptionsAction));

    public static DbContextOptionsBuilder<TContext> UseAdoNetDuckDb<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetDuckDb(connectionString, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetDuckDb<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetDuckDb(
            connection, contextOwnsConnection, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetDuckDb<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetDuckDb(dataSource, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetDuckDb<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetDuckDb(adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(DuckDBClientFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<DuckDBClientFactory>(DuckDbDialect.Instance);

    public static DbContextOptionsBuilder<TContext> UseAdoNetDuckDb<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbProviderFactory providerFactory,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetDuckDb(
            providerFactory, connectionString, adoNetOptionsAction);
}

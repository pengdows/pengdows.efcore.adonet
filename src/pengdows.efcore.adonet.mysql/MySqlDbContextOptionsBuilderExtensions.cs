using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL through MySqlConnectorFactory, with the MySqlDialect: from a connection string, a connection, a data source, or a connection string
///     or connection set later through <c>Database</c>. <c>adoNetOptionsAction</c> runs after the dialect is set.
/// </summary>
public static class MySqlDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseAdoNetMySql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(MySqlConnectorFactory.Instance, connectionString, MySqlDialect.Instance, adoNetOptionsAction);

    /// <summary>Over <paramref name="connection" />, which the caller owns unless <paramref name="contextOwnsConnection" />.</summary>
    public static DbContextOptionsBuilder UseAdoNetMySql(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(connection, contextOwnsConnection, AdoNetProviderOptions.Configure(MySqlDialect.Instance, adoNetOptionsAction));

    /// <summary>Connections from <paramref name="dataSource" />, which the caller owns.</summary>
    public static DbContextOptionsBuilder UseAdoNetMySql(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(dataSource, MySqlDialect.Instance, adoNetOptionsAction);

    /// <summary>The connection string, or connection, is set later through <c>Database</c>.</summary>
    public static DbContextOptionsBuilder UseAdoNetMySql(
        this DbContextOptionsBuilder optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(MySqlConnectorFactory.Instance, AdoNetProviderOptions.Configure(MySqlDialect.Instance, adoNetOptionsAction));

    public static DbContextOptionsBuilder<TContext> UseAdoNetMySql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetMySql(connectionString, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetMySql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetMySql(
            connection, contextOwnsConnection, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetMySql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetMySql(dataSource, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetMySql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetMySql(adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(MySqlConnectorFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<MySqlConnectorFactory>(MySqlDialect.Instance);

}

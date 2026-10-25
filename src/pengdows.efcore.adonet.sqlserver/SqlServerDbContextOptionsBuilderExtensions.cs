using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     SQL Server through SqlClientFactory, with the SqlServerDialect: from a connection string, a connection, a data source, or a connection string
///     or connection set later through <c>Database</c>. <c>adoNetOptionsAction</c> runs after the dialect is set.
/// </summary>
public static class SqlServerDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseAdoNetSqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(SqlClientFactory.Instance, connectionString, SqlServerDialect.Instance, adoNetOptionsAction);

    /// <summary>Over <paramref name="connection" />, which the caller owns unless <paramref name="contextOwnsConnection" />.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(connection, contextOwnsConnection, With(adoNetOptionsAction));

    /// <summary>Connections from <paramref name="dataSource" />, which the caller owns.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(dataSource, SqlServerDialect.Instance, adoNetOptionsAction);

    /// <summary>The connection string, or connection, is set later through <c>Database</c>.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(SqlClientFactory.Instance, With(adoNetOptionsAction));

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlServer(connectionString, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlServer(
            connection, contextOwnsConnection, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlServer(dataSource, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlServer(adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(SqlClientFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<SqlClientFactory>(SqlServerDialect.Instance);

    private static Action<AdoNetDbContextOptionsBuilder> With(Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction)
        => o =>
        {
            o.UseDialect(SqlServerDialect.Instance);
            adoNetOptionsAction?.Invoke(o);
        };
}

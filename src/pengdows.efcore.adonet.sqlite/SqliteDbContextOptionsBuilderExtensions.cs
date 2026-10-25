using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.sqlite;

/// <summary>
///     SQLite through SqliteFactory, with the SqliteDialect: from a connection string, a connection, a data source, or a connection string
///     or connection set later through <c>Database</c>. <c>adoNetOptionsAction</c> runs after the dialect is set.
/// </summary>
public static class SqliteDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseAdoNetSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(SqliteFactory.Instance, connectionString, SqliteDialect.Instance, adoNetOptionsAction);

    /// <summary>Over <paramref name="connection" />, which the caller owns unless <paramref name="contextOwnsConnection" />.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(connection, contextOwnsConnection, With(adoNetOptionsAction));

    /// <summary>Connections from <paramref name="dataSource" />, which the caller owns.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(dataSource, SqliteDialect.Instance, adoNetOptionsAction);

    /// <summary>The connection string, or connection, is set later through <c>Database</c>.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(SqliteFactory.Instance, With(adoNetOptionsAction));

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlite(connectionString, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlite(
            connection, contextOwnsConnection, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlite(dataSource, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlite(adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(SqliteFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<SqliteFactory>(SqliteDialect.Instance);

    private static Action<AdoNetDbContextOptionsBuilder> With(Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction)
        => o =>
        {
            o.UseDialect(SqliteDialect.Instance);
            adoNetOptionsAction?.Invoke(o);
        };
}

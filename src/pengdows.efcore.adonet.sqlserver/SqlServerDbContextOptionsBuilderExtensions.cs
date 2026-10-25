using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.sqlserver;

public static class SqlServerDbContextOptionsBuilderExtensions
{
    /// <summary>SqlServer through <see cref="SqlClientFactory" />; <paramref name="adoNetOptionsAction" /> runs after the dialect is set.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            SqlClientFactory.Instance,
            connectionString,
            o =>
            {
                o.UseDialect(SqlServerDialect.Instance);
                adoNetOptionsAction?.Invoke(o);
            });

    /// <summary>SqlServer over <paramref name="connection" />, which the caller owns unless <paramref name="contextOwnsConnection" />.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null,
        bool contextOwnsConnection = false)
        => optionsBuilder.UseAdoNet(connection, contextOwnsConnection, o =>
        {
            o.UseDialect(SqlServerDialect.Instance);
            adoNetOptionsAction?.Invoke(o);
        });

    /// <summary>SqlServer, with the connection string or connection set later through <c>Database</c>.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlServer(
        this DbContextOptionsBuilder optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(SqlClientFactory.Instance, o =>
        {
            o.UseDialect(SqlServerDialect.Instance);
            adoNetOptionsAction?.Invoke(o);
        });

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlServer(
            connectionString, adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(SqlClientFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<SqlClientFactory>(SqlServerDialect.Instance);
}

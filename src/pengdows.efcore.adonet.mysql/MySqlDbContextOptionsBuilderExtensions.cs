using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.mysql;

public static class MySqlDbContextOptionsBuilderExtensions
{
    /// <summary>MySql through <see cref="MySqlConnectorFactory" />; <paramref name="adoNetOptionsAction" /> runs after the dialect is set.</summary>
    public static DbContextOptionsBuilder UseAdoNetMySql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            MySqlConnectorFactory.Instance,
            connectionString,
            o =>
            {
                o.UseDialect(MySqlDialect.Instance);
                adoNetOptionsAction?.Invoke(o);
            });

    public static DbContextOptionsBuilder<TContext> UseAdoNetMySql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetMySql(
            connectionString, adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(MySqlConnectorFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<MySqlConnectorFactory>(MySqlDialect.Instance);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.sqlite;

public static class SqliteDbContextOptionsBuilderExtensions
{
    /// <summary>Sqlite through <see cref="SqliteFactory" />; <paramref name="adoNetOptionsAction" /> runs after the dialect is set.</summary>
    public static DbContextOptionsBuilder UseAdoNetSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            SqliteFactory.Instance,
            connectionString,
            o =>
            {
                o.UseDialect(SqliteDialect.Instance);
                adoNetOptionsAction?.Invoke(o);
            });

    public static DbContextOptionsBuilder<TContext> UseAdoNetSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetSqlite(
            connectionString, adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(SqliteFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<SqliteFactory>(SqliteDialect.Instance);
}

using Microsoft.EntityFrameworkCore;
using Npgsql;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.postgresql;

public static class PostgreSqlDbContextOptionsBuilderExtensions
{
    /// <summary>PostgreSql through <see cref="NpgsqlFactory" />; <paramref name="adoNetOptionsAction" /> runs after the dialect is set.</summary>
    public static DbContextOptionsBuilder UseAdoNetPostgreSql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            NpgsqlFactory.Instance,
            connectionString,
            o =>
            {
                o.UseDialect(PostgreSqlDialect.Instance);
                adoNetOptionsAction?.Invoke(o);
            });

    public static DbContextOptionsBuilder<TContext> UseAdoNetPostgreSql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetPostgreSql(
            connectionString, adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(NpgsqlFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<NpgsqlFactory>(PostgreSqlDialect.Instance);
}

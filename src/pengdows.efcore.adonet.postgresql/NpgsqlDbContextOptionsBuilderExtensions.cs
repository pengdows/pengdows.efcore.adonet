using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.postgresql;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
///     Npgsql's <c>UseNpgsql</c>, the entry point EF PostgreSQL providers expose: the same four forms as
///     <c>UseAdoNetPostgreSql</c>, over this provider. <c>npgsqlOptionsAction</c> gets an <see cref="NpgsqlDbContextOptionsBuilder" />.
/// </summary>
public static class NpgsqlDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseNpgsql(
        this DbContextOptionsBuilder optionsBuilder,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        => optionsBuilder.UseAdoNetPostgreSql(Adapt(npgsqlOptionsAction));

    public static DbContextOptionsBuilder UseNpgsql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        => optionsBuilder.UseAdoNetPostgreSql(connectionString, Adapt(npgsqlOptionsAction));

    public static DbContextOptionsBuilder UseNpgsql(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        => optionsBuilder.UseAdoNetPostgreSql(connection, false, Adapt(npgsqlOptionsAction));

    public static DbContextOptionsBuilder UseNpgsql(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        => optionsBuilder.UseAdoNetPostgreSql(connection, contextOwnsConnection, Adapt(npgsqlOptionsAction));

    public static DbContextOptionsBuilder UseNpgsql(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        => optionsBuilder.UseAdoNetPostgreSql(dataSource, Adapt(npgsqlOptionsAction));

    public static DbContextOptionsBuilder<TContext> UseNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseNpgsql(npgsqlOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseNpgsql(connectionString, npgsqlOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseNpgsql(connection, npgsqlOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseNpgsql(connection, contextOwnsConnection, npgsqlOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseNpgsql(dataSource, npgsqlOptionsAction);

    private static Action<AdoNetDbContextOptionsBuilder>? Adapt(Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction)
        => npgsqlOptionsAction is null ? null : options => npgsqlOptionsAction(new NpgsqlDbContextOptionsBuilder(options));
}

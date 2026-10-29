using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     PostgreSQL through NpgsqlFactory, with the PostgreSqlDialect: from a connection string, a connection, a data source, or a connection string
///     or connection set later through <c>Database</c>. <c>adoNetOptionsAction</c> runs after the dialect is set.
/// </summary>
public static class PostgreSqlDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseAdoNetPostgreSql(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            NpgsqlFactory.Instance,
            connectionString,
            PostgreSqlDialect.Instance,
            AdoNetProviderOptions.Configure(PostgreSqlDialect.Instance, adoNetOptionsAction, ParameterTranslationMode.Parameter));

    /// <summary>
    ///     The same PostgreSQL dialect over any driver: <paramref name="providerFactory" /> supplies the connections (Npgsql is the
    ///     default). This is how a provider's driver is swapped, as MySqlConnector and MySql.Data are for MySQL.
    /// </summary>
    public static DbContextOptionsBuilder UseAdoNetPostgreSql(
        this DbContextOptionsBuilder optionsBuilder,
        DbProviderFactory providerFactory,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            providerFactory,
            connectionString,
            PostgreSqlDialect.Instance,
            AdoNetProviderOptions.Configure(PostgreSqlDialect.Instance, adoNetOptionsAction, ParameterTranslationMode.Parameter));

    /// <summary>Over <paramref name="connection" />, which the caller owns unless <paramref name="contextOwnsConnection" />.</summary>
    public static DbContextOptionsBuilder UseAdoNetPostgreSql(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(connection, contextOwnsConnection, AdoNetProviderOptions.Configure(PostgreSqlDialect.Instance, adoNetOptionsAction, ParameterTranslationMode.Parameter));

    /// <summary>Connections from <paramref name="dataSource" />, which the caller owns.</summary>
    public static DbContextOptionsBuilder UseAdoNetPostgreSql(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            dataSource,
            PostgreSqlDialect.Instance,
            AdoNetProviderOptions.Configure(PostgreSqlDialect.Instance, adoNetOptionsAction, ParameterTranslationMode.Parameter));

    /// <summary>The connection string, or connection, is set later through <c>Database</c>.</summary>
    public static DbContextOptionsBuilder UseAdoNetPostgreSql(
        this DbContextOptionsBuilder optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(NpgsqlFactory.Instance, AdoNetProviderOptions.Configure(PostgreSqlDialect.Instance, adoNetOptionsAction, ParameterTranslationMode.Parameter));

    public static DbContextOptionsBuilder<TContext> UseAdoNetPostgreSql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetPostgreSql(connectionString, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetPostgreSql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetPostgreSql(
            connection, contextOwnsConnection, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetPostgreSql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetPostgreSql(dataSource, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNetPostgreSql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetPostgreSql(adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(NpgsqlFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<NpgsqlFactory>(PostgreSqlDialect.Instance);

    public static DbContextOptionsBuilder<TContext> UseAdoNetPostgreSql<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbProviderFactory providerFactory,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetPostgreSql(
            providerFactory, connectionString, adoNetOptionsAction);
}

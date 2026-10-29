using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet;

public static class AdoNetDbContextOptionsBuilderExtensions
{
    /// <summary>
    ///     Uses <paramref name="providerFactory" /> to create a connection per context (pooling is the provider's).
    ///     Without <c>UseDialect</c>, the dialect registered for the factory in <see cref="AdoNetDialects" /> is used; with
    ///     neither, the context refuses to start.
    /// </summary>
    public static DbContextOptionsBuilder UseAdoNet(
        this DbContextOptionsBuilder optionsBuilder,
        DbProviderFactory providerFactory,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var extension = GetOrCreateExtension(optionsBuilder).WithProviderFactory(providerFactory);

        return Apply(optionsBuilder, (AdoNetOptionsExtension)extension.WithConnectionString(connectionString), adoNetOptionsAction);
    }

    /// <summary>
    ///     A factory whose connection string, or connection, is supplied later through
    ///     <c>Database.SetConnectionString</c> or <c>Database.SetDbConnection</c>. A connection or data source configured
    ///     before stays.
    /// </summary>
    public static DbContextOptionsBuilder UseAdoNet(
        this DbContextOptionsBuilder optionsBuilder,
        DbProviderFactory providerFactory,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);

        // Names the driver for a connection supplied later, so a connection or data source configured earlier stays, as
        // EF's own providers keep one when configured again with no connection string.
        var extension = GetOrCreateExtension(optionsBuilder);
        return Apply(
            optionsBuilder,
            extension.Connection != null || extension.DataSource != null ? extension : extension.WithProviderFactory(providerFactory),
            adoNetOptionsAction);
    }

    /// <summary>
    ///     The whole database integration in two arguments: the ADO.NET factory and the dialect. Switching databases
    ///     changes only these.
    /// </summary>
    public static DbContextOptionsBuilder UseAdoNet(
        this DbContextOptionsBuilder optionsBuilder,
        DbProviderFactory providerFactory,
        string connectionString,
        IAdoNetDialect dialect,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(dialect);

        return optionsBuilder.UseAdoNet(
            providerFactory,
            connectionString,
            o =>
            {
                o.UseDialect(dialect);
                adoNetOptionsAction?.Invoke(o);
            });
    }

    /// <summary>Resolves the factory from <see cref="DbProviderFactories" /> so the provider can be chosen by configuration.</summary>
    public static DbContextOptionsBuilder UseAdoNet(
        this DbContextOptionsBuilder optionsBuilder,
        string providerInvariantName,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(DbProviderFactories.GetFactory(providerInvariantName), connectionString, adoNetOptionsAction);

    /// <summary>
    ///     Connections come from <paramref name="dataSource" />, which the caller owns and disposes. Without <c>UseDialect</c>,
    ///     one connection is created, not opened, to find the dialect registered for its provider.
    /// </summary>
    public static DbContextOptionsBuilder UseAdoNet(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return Apply(optionsBuilder, GetOrCreateExtension(optionsBuilder).WithDataSource(dataSource), adoNetOptionsAction);
    }

    /// <summary>Connections come from <paramref name="dataSource" />, which the caller owns and disposes, in <paramref name="dialect" />.</summary>
    public static DbContextOptionsBuilder UseAdoNet(
        this DbContextOptionsBuilder optionsBuilder,
        DbDataSource dataSource,
        IAdoNetDialect dialect,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(dialect);

        return optionsBuilder.UseAdoNet(
            dataSource,
            o =>
            {
                o.UseDialect(dialect);
                adoNetOptionsAction?.Invoke(o);
            });
    }

    public static DbContextOptionsBuilder UseAdoNet(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var extension = GetOrCreateExtension(optionsBuilder).WithConnection(connection, contextOwnsConnection);

        return Apply(optionsBuilder, (AdoNetOptionsExtension)extension, adoNetOptionsAction);
    }

    public static DbContextOptionsBuilder<TContext> UseAdoNet<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbProviderFactory providerFactory,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNet(providerFactory, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNet<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbProviderFactory providerFactory,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNet(
            providerFactory, connectionString, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNet<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbProviderFactory providerFactory,
        string connectionString,
        IAdoNetDialect dialect,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNet(
            providerFactory, connectionString, dialect, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNet<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string providerInvariantName,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNet(
            providerInvariantName, connectionString, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNet<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNet(dataSource, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNet<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbDataSource dataSource,
        IAdoNetDialect dialect,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNet(dataSource, dialect, adoNetOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseAdoNet<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNet(
            connection, contextOwnsConnection, adoNetOptionsAction);

    private static AdoNetOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<AdoNetOptionsExtension>() ?? new AdoNetOptionsExtension();

    private static DbContextOptionsBuilder Apply(
        DbContextOptionsBuilder optionsBuilder,
        AdoNetOptionsExtension extension,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction)
    {
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        var coreOptionsExtension = optionsBuilder.Options.FindExtension<CoreOptionsExtension>() ?? new CoreOptionsExtension();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
            RelationalOptionsExtension.WithDefaultWarningConfiguration(coreOptionsExtension));

        adoNetOptionsAction?.Invoke(new AdoNetDbContextOptionsBuilder(optionsBuilder));

        return optionsBuilder;
    }
}

using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     A plugin's <c>UseAdoNet...</c> entry points, for <see cref="EntryPointConformanceTests{TEntryPoints}" />. Nothing here
///     opens a connection, so no database is needed.
/// </summary>
public abstract class AdoNetEntryPoints
{
    /// <summary>The dialect every entry point configures.</summary>
    public abstract IAdoNetDialect Dialect { get; }

    /// <summary>The driver's factory, for a connection and a data source.</summary>
    public abstract DbProviderFactory Factory { get; }

    /// <summary>A connection string the driver accepts; it is never opened.</summary>
    public abstract string ConnectionString { get; }

    public abstract DbContextOptionsBuilder<BlogContext> UseConnectionString(DbContextOptionsBuilder<BlogContext> builder, string connectionString);

    public abstract DbContextOptionsBuilder UseConnectionString(DbContextOptionsBuilder builder, string connectionString);

    public abstract DbContextOptionsBuilder<BlogContext> UseConnection(
        DbContextOptionsBuilder<BlogContext> builder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AdoNetDbContextOptionsBuilder> configure);

    public abstract DbContextOptionsBuilder<BlogContext> UseDataSource(DbContextOptionsBuilder<BlogContext> builder, DbDataSource dataSource);

    /// <summary>No connection yet: one is set later.</summary>
    public abstract DbContextOptionsBuilder<BlogContext> UseNoConnection(DbContextOptionsBuilder<BlogContext> builder);
}

/// <summary>
///     Every entry point configures the plugin's dialect, with the core's argument order: a connection string, a connection
///     (owned or borrowed, with options), a data source, a connection string set later, and the non-generic builder.
/// </summary>
public abstract class EntryPointConformanceTests<TEntryPoints>
    where TEntryPoints : AdoNetEntryPoints, new()
{
    private readonly TEntryPoints _entryPoints = new();

    private void UsesTheDialect(DbContextOptions<BlogContext> options)
    {
        using var context = new BlogContext(options);
        Assert.Same(_entryPoints.Dialect, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void A_connection_string()
        => UsesTheDialect(_entryPoints.UseConnectionString(new DbContextOptionsBuilder<BlogContext>().AllowManyServiceProviders(), _entryPoints.ConnectionString).Options);

    [Fact]
    public void A_connection_the_context_may_own()
    {
        using var connection = _entryPoints.Factory.CreateConnection()!;
        connection.ConnectionString = _entryPoints.ConnectionString;
        var options = _entryPoints.UseConnection(new DbContextOptionsBuilder<BlogContext>().AllowManyServiceProviders(), connection, false, o => o.CommandTimeout(5)).Options;

        UsesTheDialect(options);
        using var context = new BlogContext(options);
        Assert.Same(connection, context.Database.GetDbConnection());
        Assert.Equal(5, context.Database.GetCommandTimeout());
    }

    [Fact]
    public void A_data_source()
    {
        using var dataSource = _entryPoints.Factory.CreateDataSource(_entryPoints.ConnectionString);

        UsesTheDialect(_entryPoints.UseDataSource(new DbContextOptionsBuilder<BlogContext>().AllowManyServiceProviders(), dataSource).Options);
    }

    [Fact]
    public void A_connection_string_set_later()
    {
        using var context = new BlogContext(_entryPoints.UseNoConnection(new DbContextOptionsBuilder<BlogContext>().AllowManyServiceProviders()).Options);
        context.Database.SetConnectionString(_entryPoints.ConnectionString);

        Assert.Same(_entryPoints.Dialect, context.GetService<IAdoNetDialect>());
        Assert.Equal(_entryPoints.ConnectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void The_non_generic_builder_works_too()
    {
        var builder = new DbContextOptionsBuilder<BlogContext>().AllowManyServiceProviders();
        _entryPoints.UseConnectionString((DbContextOptionsBuilder)builder, _entryPoints.ConnectionString);

        UsesTheDialect(builder.Options);
    }

    // What dotnet ef dbcontext scaffold writes into OnConfiguring: the design-time services EF's tools find through the
    // plugin assembly's DesignTimeProviderServices attribute generate a Use call that, run as written, configures the plugin.
    [Fact]
    public void Scaffolded_code_configures_the_plugin()
    {
        var assembly = _entryPoints.Dialect.GetType().Assembly;
        var attribute = assembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>();
        Assert.NotNull(attribute);
        var designTimeServices = (IDesignTimeServices)Activator.CreateInstance(assembly.GetType(attribute.TypeName, throwOnError: true)!)!;
        var services = new ServiceCollection();
        designTimeServices.ConfigureDesignTimeServices(services);
        using var serviceProvider = services.BuildServiceProvider();

        var call = serviceProvider.GetRequiredService<IProviderConfigurationCodeGenerator>().GenerateUseProvider(_entryPoints.ConnectionString);

        Assert.Equal([_entryPoints.ConnectionString], call.Arguments);
        Assert.Null(call.ChainedCall);
        var builder = new DbContextOptionsBuilder<BlogContext>().AllowManyServiceProviders();
        Assert.NotNull(call.MethodInfo);
        call.MethodInfo.Invoke(null, [builder, .. call.Arguments, .. call.MethodInfo.GetParameters().Skip(1 + call.Arguments.Count).Select(p => p.DefaultValue)]);
        UsesTheDialect(builder.Options);
    }
}

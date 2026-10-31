using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet;
using pengdows.efcore.adonet.Infrastructure;

namespace OutsidePlugin;

// The UseAdoNetXxx entry point every first-party plugin has, built on AdoNetProviderOptions, which is public now.
public static class ExampleDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseAdoNetExample(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            connection,
            contextOwnsConnection,
            AdoNetProviderOptions.Configure(ExampleDialect.Instance, adoNetOptionsAction));

    public static DbContextOptionsBuilder<TContext> UseAdoNetExample<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder)
            .UseAdoNetExample(connection, contextOwnsConnection, adoNetOptionsAction);
}

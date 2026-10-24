using Microsoft.EntityFrameworkCore;
using pengdows.flatfile;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.flatfile;

public static class FlatFileDbContextOptionsBuilderExtensions
{
    /// <summary>FlatFile through <see cref="FlatFileProviderFactory" />; <paramref name="adoNetOptionsAction" /> runs after the dialect is set.</summary>
    public static DbContextOptionsBuilder UseAdoNetFlatFile(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            FlatFileProviderFactory.Instance,
            connectionString,
            o =>
            {
                o.UseDialect(FlatFileDialect.Instance);
                adoNetOptionsAction?.Invoke(o);
            });

    public static DbContextOptionsBuilder<TContext> UseAdoNetFlatFile<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetFlatFile(
            connectionString, adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(FlatFileProviderFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<FlatFileProviderFactory>(FlatFileDialect.Instance);
}

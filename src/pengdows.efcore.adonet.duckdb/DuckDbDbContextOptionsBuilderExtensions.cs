using Microsoft.EntityFrameworkCore;
using DuckDB.NET.Data;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.duckdb;

public static class DuckDbDbContextOptionsBuilderExtensions
{
    /// <summary>DuckDb through <see cref="DuckDBClientFactory" />; <paramref name="adoNetOptionsAction" /> runs after the dialect is set.</summary>
    public static DbContextOptionsBuilder UseAdoNetDuckDb(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        => optionsBuilder.UseAdoNet(
            DuckDBClientFactory.Instance,
            connectionString,
            o =>
            {
                o.UseDialect(DuckDbDialect.Instance);
                adoNetOptionsAction?.Invoke(o);
            });

    public static DbContextOptionsBuilder<TContext> UseAdoNetDuckDb<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<AdoNetDbContextOptionsBuilder>? adoNetOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseAdoNetDuckDb(
            connectionString, adoNetOptionsAction);

    /// <summary>Makes <c>UseAdoNet(DuckDBClientFactory.Instance, ...)</c> pick this dialect without <c>UseDialect</c>.</summary>
    public static void RegisterDialect()
        => AdoNetDialects.Register<DuckDBClientFactory>(DuckDbDialect.Instance);
}

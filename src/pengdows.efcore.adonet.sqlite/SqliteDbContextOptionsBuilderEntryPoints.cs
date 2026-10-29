using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.sqlite;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
///     Microsoft's <c>UseSqlite</c>, the entry point EF SQLite providers expose: the same forms as <c>UseAdoNetSqlite</c>, over
///     this provider. <c>sqliteOptionsAction</c> gets a <see cref="SqliteDbContextOptionsBuilder" />.
/// </summary>
public static class SqliteDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        => optionsBuilder.UseAdoNetSqlite(Adapt(sqliteOptionsAction));

    public static DbContextOptionsBuilder UseSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        => optionsBuilder.UseAdoNetSqlite(connectionString, Adapt(sqliteOptionsAction));

    public static DbContextOptionsBuilder UseSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        => optionsBuilder.UseAdoNetSqlite(connection, false, Adapt(sqliteOptionsAction));

    public static DbContextOptionsBuilder UseSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        => optionsBuilder.UseAdoNetSqlite(connection, contextOwnsConnection, Adapt(sqliteOptionsAction));

    public static DbContextOptionsBuilder<TContext> UseSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseSqlite(sqliteOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string connectionString,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseSqlite(connectionString, sqliteOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseSqlite(connection, sqliteOptionsAction);

    public static DbContextOptionsBuilder<TContext> UseSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)optionsBuilder).UseSqlite(connection, contextOwnsConnection, sqliteOptionsAction);

    private static Action<AdoNetDbContextOptionsBuilder>? Adapt(Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction)
        => sqliteOptionsAction is null ? null : options => sqliteOptionsAction(new SqliteDbContextOptionsBuilder(options));
}

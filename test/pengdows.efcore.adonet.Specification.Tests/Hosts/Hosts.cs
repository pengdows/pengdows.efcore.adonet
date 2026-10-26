using DuckDB.NET.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using pengdows.efcore.adonet.duckdb;
using pengdows.efcore.adonet.flatfile;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.mysql;
using pengdows.efcore.adonet.postgresql;
using pengdows.efcore.adonet.sqlite;
using pengdows.efcore.adonet.sqlserver;
using pengdows.flatfile;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>A server: one container for the run, a database per test store, made by the server's own DDL.</summary>
public abstract class ServerHost : IDatabaseHost
{
    protected abstract string AdminConnectionString { get; }
    protected abstract IEnumerable<string> RecreateSql(string database);
    public abstract string ConnectionStringFor(string database);
    public abstract DbConnection CreateConnection(string connectionString);
    public abstract DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure);
    public abstract DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure);
    public abstract IAdoNetDialect Dialect { get; }

    public async Task CreateEmptyDatabaseAsync(string database)
    {
        await using var connection = CreateConnection(AdminConnectionString);
        await connection.OpenAsync();
        foreach (var sql in RecreateSql(database))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class SqlServerHost : ServerHost
{
    private static readonly RunContainer Container = new(() => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build());

    protected override string AdminConnectionString => Container.ConnectionString;
    public override IAdoNetDialect Dialect => SqlServerDialect.Instance;

    protected override IEnumerable<string> RecreateSql(string database)
        => [$"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END", $"CREATE DATABASE [{database}]"];

    public override string ConnectionStringFor(string database)
        => new SqlConnectionStringBuilder(Container.ConnectionString) { InitialCatalog = database }.ConnectionString;

    public override DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetSqlServer(connectionString, OneInsertPerCommand(configure)).AddInterceptors(SqlServerIdentityInsert.Instance);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetSqlServer(connection, contextOwnsConnection: false, OneInsertPerCommand(configure)).AddInterceptors(SqlServerIdentityInsert.Instance);

    // SqlServerIdentityInsert turns IDENTITY_INSERT on for one table at a time.
    private static Action<AdoNetDbContextOptionsBuilder> OneInsertPerCommand(Action<AdoNetDbContextOptionsBuilder> configure)
        => o =>
        {
            configure(o);
            o.MaxBatchSize(1);
        };
}

public sealed class PostgreSqlHost : ServerHost
{
    private static readonly RunContainer Container = new(() => new PostgreSqlBuilder("postgres:17").Build());

    protected override string AdminConnectionString => Container.ConnectionString;
    public override IAdoNetDialect Dialect => PostgreSqlDialect.Instance;

    protected override IEnumerable<string> RecreateSql(string database)
        => [$"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", $"CREATE DATABASE \"{database}\""];

    public override string ConnectionStringFor(string database)
        => new Npgsql.NpgsqlConnectionStringBuilder(Container.ConnectionString) { Database = database, MaxPoolSize = 22 }.ConnectionString;

    public override DbConnection CreateConnection(string connectionString) => new Npgsql.NpgsqlConnection(connectionString);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetPostgreSql(connectionString, configure);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetPostgreSql(connection, contextOwnsConnection: false, configure);
}

public sealed class MySqlHost : ServerHost
{
    private static readonly RunContainer Container = new(() => new MySqlBuilder("mysql:8.4").Build());

    // Root, so each test store can have its own database.
    protected override string AdminConnectionString
        => new MySqlConnectionStringBuilder(Container.ConnectionString) { UserID = "root", Database = "" }.ConnectionString;

    public override IAdoNetDialect Dialect => MySqlDialect.Instance;

    protected override IEnumerable<string> RecreateSql(string database)
        => [$"DROP DATABASE IF EXISTS `{database}`", $"CREATE DATABASE `{database}`"];

    public override string ConnectionStringFor(string database)
        => new MySqlConnectionStringBuilder(AdminConnectionString) { Database = database }.ConnectionString;

    public override DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetMySql(connectionString, configure);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetMySql(connection, contextOwnsConnection: false, configure);
}

/// <summary>An embedded database: a file or folder per test store, under one temporary directory for the run.</summary>
public abstract class FileHost : IDatabaseHost
{
    protected static readonly string Root = Directory.CreateTempSubdirectory("adonet-spec-").FullName;

    public abstract string ConnectionStringFor(string database);
    public abstract Task CreateEmptyDatabaseAsync(string database);
    public abstract DbConnection CreateConnection(string connectionString);
    public abstract DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure);
    public abstract DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure);
    public abstract IAdoNetDialect Dialect { get; }
}

public sealed class SqliteHost : FileHost
{
    public override IAdoNetDialect Dialect => SqliteDialect.Instance;

    public override string ConnectionStringFor(string database) => $"Data Source={Path.Combine(Root, database + ".db")}";

    public override Task CreateEmptyDatabaseAsync(string database)
    {
        SqliteConnection.ClearAllPools();
        File.Delete(Path.Combine(Root, database + ".db"));
        return Task.CompletedTask;
    }

    public override DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetSqlite(connectionString, configure);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetSqlite(connection, contextOwnsConnection: false, configure);
}

public sealed class DuckDbHost : FileHost
{
    public override IAdoNetDialect Dialect => DuckDbDialect.Instance;

    public override string ConnectionStringFor(string database) => $"Data Source={Path.Combine(Root, database + ".duckdb")}";

    public override Task CreateEmptyDatabaseAsync(string database)
    {
        File.Delete(Path.Combine(Root, database + ".duckdb"));
        File.Delete(Path.Combine(Root, database + ".duckdb.wal"));
        return Task.CompletedTask;
    }

    public override DbConnection CreateConnection(string connectionString) => new DuckDBConnection(connectionString);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetDuckDb(connectionString, configure);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetDuckDb(connection, contextOwnsConnection: false, configure);
}

public sealed class FlatFileHost : FileHost
{
    public override IAdoNetDialect Dialect => FlatFileDialect.Instance;

    public override string ConnectionStringFor(string database) => $"path={Path.Combine(Root, database)}";

    public override Task CreateEmptyDatabaseAsync(string database)
    {
        var path = Path.Combine(Root, database);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public override DbConnection CreateConnection(string connectionString) => new FlatFileConnection { ConnectionString = connectionString };

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetFlatFile(connectionString, configure);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetFlatFile(connection, contextOwnsConnection: false, configure);
}

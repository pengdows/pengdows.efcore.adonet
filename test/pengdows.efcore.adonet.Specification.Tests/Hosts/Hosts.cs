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
    public virtual TimeSpan TemporalResolution => TimeSpan.FromTicks(1);
    public virtual bool KeepsOffsets => true;
    public virtual NorthwindProcedures? Procedures => null;
    public virtual string? NonDefaultCollation => null;

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
    // timestamp, time and interval keep microseconds.
    public override TimeSpan TemporalResolution => TimeSpan.FromMicroseconds(1);

    // A DateTimeOffset is a timestamptz instant, read back at offset zero.
    public override bool KeepsOffsets => false;

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
    // DATETIME(6) and TIME(6) keep microseconds.
    public override TimeSpan TemporalResolution => TimeSpan.FromMicroseconds(1);

    // A DateTimeOffset is stored as its UTC instant in a DATETIME(6), read back at offset zero.
    public override bool KeepsOffsets => false;

    // The test databases are utf8mb4_0900_as_cs.
    public override string NonDefaultCollation => "utf8mb4_bin";

    // As Pomelo's Northwind script defines them; the parameter's collation yields to the column's (as a _bin one does), where
    // the character set's default (utf8mb4_0900_ai_ci) would clash with it. CALL reports no rows affected. Behaviour reference
    // (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/Northwind.sql
    public override NorthwindProcedures Procedures { get; } = new(
        [
            "CREATE PROCEDURE `Ten Most Expensive Products`() SELECT `ProductName` AS `TenMostExpensiveProducts`, `UnitPrice` "
            + "FROM `Products` ORDER BY `UnitPrice` DESC LIMIT 10",
            "CREATE PROCEDURE `CustOrderHist`(pCustomerID CHAR(5) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin) SELECT `ProductName`, SUM(`Quantity`) AS `Total` "
            + "FROM `Products` AS p, `OrderDetails` AS od, `Orders` AS o, `Customers` AS c WHERE c.`CustomerID` = pCustomerID "
            + "AND c.`CustomerID` = o.`CustomerID` AND o.`OrderID` = od.`OrderID` AND od.`ProductID` = p.`ProductID` GROUP BY `ProductName`"
        ],
        "CALL `Ten Most Expensive Products`()",
        "CALL `CustOrderHist`({0})",
        "CALL `CustOrderHist`(@CustomerID)",
        CallRowsAffected: 0);

    private static readonly RunContainer Container = new(() => new MySqlBuilder("mysql:8.4").Build());

    // Root, so each test store can have its own database.
    protected override string AdminConnectionString
        => new MySqlConnectionStringBuilder(Container.ConnectionString) { UserID = "root", Database = "" }.ConnectionString;

    public override IAdoNetDialect Dialect => MySqlDialect.Instance;

    // Case- and accent-sensitive, as EF's expected results compare strings ordinally; the server's default collation
    // (utf8mb4_0900_ai_ci) ignores both. Pomelo's test databases do the same. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/TestUtilities/MySqlTestStore.cs
    protected override IEnumerable<string> RecreateSql(string database)
        => [$"DROP DATABASE IF EXISTS `{database}`", $"CREATE DATABASE `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_cs"];

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
    public virtual TimeSpan TemporalResolution => TimeSpan.FromTicks(1);
    public virtual bool KeepsOffsets => true;
    public virtual NorthwindProcedures? Procedures => null;
    public virtual string? NonDefaultCollation => null;

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
    // TIMESTAMP and TIME keep microseconds.
    public override TimeSpan TemporalResolution => TimeSpan.FromMicroseconds(1);

    // A DateTimeOffset is a TIMESTAMPTZ instant, read back at offset zero.
    public override bool KeepsOffsets => false;

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

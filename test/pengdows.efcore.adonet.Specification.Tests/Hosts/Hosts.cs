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
using pengdows.stormgate;
using pengdows.stormgate.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>A server: one container for the run, a database per test store, made by the server's own DDL.</summary>
public abstract class ServerHost : IDatabaseHost
{
    public virtual bool KeepStoreConnectionOpen => true;
    public virtual bool SupportsMalformedJsonProperties => true;
    public virtual TimeSpan TemporalResolution => TimeSpan.FromTicks(1);
    public virtual bool KeepsOffsets => true;
    public virtual DateTime DefaultDateTime => new();
    public virtual bool StrictEquality => true;
    public virtual NorthwindProcedures? Procedures => null;
    public virtual string? NonDefaultCollation => null;
    public virtual string? CaseInsensitiveCollation => null;
    public virtual string? CaseSensitiveCollation => null;
    public virtual string? DatabaseSchema => null;
    public virtual UpdateProcedures? UpdateProcedures => null;

    protected abstract string AdminConnectionString { get; }
    protected abstract IEnumerable<string> RecreateSql(string database);
    public abstract string ConnectionStringFor(string database);
    public abstract DbConnection CreateConnection(string connectionString);
    public abstract DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure);
    public abstract DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure);
    public abstract IAdoNetDialect Dialect { get; }

    protected virtual async Task<DbConnection> OpenAdminAsync()
    {
        var connection = CreateConnection(AdminConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task CreateEmptyDatabaseAsync(string database)
    {
        await using var connection = await OpenAdminAsync();
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

    // With MultipleActiveResultSets, as Microsoft's SQL Server test store connects: EF's suites lazy-load while a query is
    // still being read. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.SqlServer.FunctionalTests/TestUtilities/SqlServerTestStore.cs
    public override string ConnectionStringFor(string database)
        => new SqlConnectionStringBuilder(Container.ConnectionString) { InitialCatalog = database, MultipleActiveResultSets = true }.ConnectionString;

    public override DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetSqlServer(connectionString, configure);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetSqlServer(connection, contextOwnsConnection: false, configure);
}

public sealed class PostgreSqlHost : ServerHost
{
    // timestamp, time and interval keep microseconds.
    public override TimeSpan TemporalResolution => TimeSpan.FromMicroseconds(1);

    // A DateTimeOffset is a timestamptz instant, read back at offset zero.
    public override bool KeepsOffsets => false;

    private static readonly RunContainer Container = new(() => new PostgreSqlBuilder("postgres:17").Build());

    // The image allows 25 connections in all and a few go to administration. Npgsql pools per connection string and each test
    // store has its own database, so a pool size can't bound the total: one StormGate holds the budget for every connection the
    // run opens, EF's and the admin ones, and pooling is off so a closed connection frees its server slot at once.
    private const int ConnectionBudget = 22;

    // A request for a permit waits up to five minutes, so a saturated gate queues the run instead of failing tests; the gate
    // belongs to the process and is released with it.
    private static readonly Lazy<StormGate> Gate = new(() =>
    {
        var gate = StormGate.Create(
            Npgsql.NpgsqlFactory.Instance, WithoutPooling(Container.ConnectionString), ConnectionBudget, TimeSpan.FromMinutes(5));
        AppDomain.CurrentDomain.ProcessExit += (_, _) => gate.Dispose();
        return gate;
    });

    private static readonly Lazy<StormGateConnectionInterceptor> Interceptor = new(() => new StormGateConnectionInterceptor(Gate.Value));

    private static string WithoutPooling(string connectionString)
        => new Npgsql.NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;

    protected override string AdminConnectionString => Container.ConnectionString;
    protected override async Task<DbConnection> OpenAdminAsync() => await Gate.Value.OpenAsync();
    public override IAdoNetDialect Dialect => PostgreSqlDialect.Instance;

    protected override IEnumerable<string> RecreateSql(string database)
        => [$"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", $"CREATE DATABASE \"{database}\""];

    public override string ConnectionStringFor(string database)
        => new Npgsql.NpgsqlConnectionStringBuilder(Container.ConnectionString) { Database = database, Pooling = false }.ConnectionString;

    public override DbConnection CreateConnection(string connectionString) => new Npgsql.NpgsqlConnection(connectionString);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetPostgreSql(connectionString, NullsFirst(configure)).UseStormGate(Interceptor.Value);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetPostgreSql(connection, contextOwnsConnection: false, NullsFirst(configure)).UseStormGate(Interceptor.Value);

    // EF's tests expect nulls first, as in .NET and SQL Server; PostgreSQL sorts them last unless told.
    private static Action<AdoNetDbContextOptionsBuilder> NullsFirst(Action<AdoNetDbContextOptionsBuilder> configure)
        => o =>
        {
            configure(o);
            o.ReverseNullOrdering();
        };
}

public class MySqlHost : ServerHost
{
    // MySQL's JSON type validates documents and canonicalizes duplicate keys, so EF's malformed-document fixture cannot be stored faithfully.
    public override bool SupportsMalformedJsonProperties => false;
    // DATETIME(6) and TIME(6) keep microseconds.
    public override TimeSpan TemporalResolution => TimeSpan.FromMicroseconds(1);

    // MySQL DATETIME cannot represent .NET's year-one sentinel used by EF's generic probe.
    public override DateTime DefaultDateTime => new(2000, 1, 1, 12, 34, 56);

    // MySQL's FLOAT/DOUBLE comparisons can round the EF probe values at storage or parameter binding time.
    public override bool StrictEquality => false;

    // A DateTimeOffset is stored as its UTC instant in a DATETIME(6), read back at offset zero.
    public override bool KeepsOffsets => false;

    // The test databases are utf8mb4_0900_as_cs.
    public override string NonDefaultCollation => "utf8mb4_bin";
    public override string CaseInsensitiveCollation => "utf8mb4_0900_ai_ci";
    public override string CaseSensitiveCollation => "utf8mb4_bin";

    public override UpdateProcedures UpdateProcedures => MySqlUpdateProcedures.Instance;

    // As Pomelo's Northwind script defines them; the parameter's collation yields to the column's (as a _bin one does), where
    // the character set's default (utf8mb4_0900_ai_ci) would clash with it. CALL reports no rows affected. Behaviour reference
    // (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/Northwind.sql
    public override NorthwindProcedures Procedures { get; } = new(
        [
            "CREATE PROCEDURE `Ten Most Expensive Products`() SELECT `ProductName` AS `TenMostExpensiveProducts`, `UnitPrice` "
            + "FROM `Products` ORDER BY `UnitPrice` DESC LIMIT 10",
            "CREATE PROCEDURE `CustOrderHist`(pCustomerID CHAR(5) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin) SELECT `ProductName`, SUM(`Quantity`) AS `Total` "
            + "FROM `Products` AS p, `Order Details` AS od, `Orders` AS o, `Customers` AS c WHERE c.`CustomerID` = pCustomerID "
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

    // Keep MySQL's default utf8mb4_0900_ai_ci collation. The shared tests request utf8mb4_bin explicitly when they need
    // ordinal comparison; this is also how Pomelo's Northwind database is configured. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/TestUtilities/MySqlTestStore.cs
    protected override IEnumerable<string> RecreateSql(string database)
        => [$"DROP DATABASE IF EXISTS `{database}`", $"CREATE DATABASE `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci"];

    // With the user variables the dialect reads procedure outputs through, which a connection opened before the provider
    // sees it must already allow.
    public override string ConnectionStringFor(string database)
    {
        var connectionString = new MySqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = database,
            AllowUserVariables = true,
            UseAffectedRows = false
        }.ConnectionString;

        // MySql.Data returns JSON numeric values as unsigned provider wrappers; disabling its TINYINT boolean
        // coercion keeps the ADO.NET value compatible with EF's byte materializer.
        return UseMySqlData
            ? new global::MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connectionString) { TreatTinyAsBoolean = false }.ConnectionString
            : connectionString;
    }

    private static bool UseMySqlData
        => string.Equals(Environment.GetEnvironmentVariable("PENGDOWS_MYSQL_DRIVER"), "MySql.Data", StringComparison.OrdinalIgnoreCase);

    public override DbConnection CreateConnection(string connectionString)
        => UseMySqlData
            ? new global::MySql.Data.MySqlClient.MySqlConnection(connectionString)
            : new MySqlConnection(connectionString);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure)
        => UseMySqlData
            ? builder.UseAdoNet(global::MySql.Data.MySqlClient.MySqlClientFactory.Instance, connectionString, MySqlDialect.Instance, configure)
            : builder.UseAdoNetMySql(connectionString, configure);

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNet(connection, contextOwnsConnection: false, o =>
        {
            o.UseDialect(MySqlDialect.Instance);
            configure(o);
        });
}

/// <summary>An embedded database: a file or folder per test store, under one temporary directory for the run.</summary>
public abstract class FileHost : IDatabaseHost
{
    public virtual bool KeepStoreConnectionOpen => false;
    public virtual bool SupportsMalformedJsonProperties => true;
    public virtual TimeSpan TemporalResolution => TimeSpan.FromTicks(1);
    public virtual bool KeepsOffsets => true;
    public virtual NorthwindProcedures? Procedures => null;
    public virtual string? NonDefaultCollation => null;
    public virtual string? CaseInsensitiveCollation => null;
    public virtual string? CaseSensitiveCollation => null;
    public virtual string? DatabaseSchema => null;
    public virtual UpdateProcedures? UpdateProcedures => null;

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
        => builder.UseAdoNetDuckDb(connectionString, NullsFirst(configure));

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetDuckDb(connection, contextOwnsConnection: false, NullsFirst(configure));

    // EF's tests expect nulls first, as in .NET and SQL Server; DuckDB sorts them last unless told.
    private static Action<AdoNetDbContextOptionsBuilder> NullsFirst(Action<AdoNetDbContextOptionsBuilder> configure)
        => o =>
        {
            configure(o);
            o.ReverseNullOrdering();
        };
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
        => builder.UseAdoNetFlatFile(connectionString, NullsFirst(configure));

    public override DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure)
        => builder.UseAdoNetFlatFile(connection, contextOwnsConnection: false, NullsFirst(configure));

    // EF's tests expect nulls first, as in .NET and SQL Server; ISO leaves the default to the implementation.
    private static Action<AdoNetDbContextOptionsBuilder> NullsFirst(Action<AdoNetDbContextOptionsBuilder> configure)
        => o =>
        {
            configure(o);
            o.ReverseNullOrdering();
        };
}

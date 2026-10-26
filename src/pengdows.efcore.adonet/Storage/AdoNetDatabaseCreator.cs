using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     <see cref="Exists" /> and <see cref="HasTables" /> work for any provider; creating and deleting a database follow the
///     dialect's <see cref="DatabaseLifecycle" />, and are not supported without one. A server database is created and dropped
///     through a second EF connection to an administrative database, so its commands are logged and intercepted like any
///     other, as EF's own server providers do; a file or directory database on disk. <see cref="Create" /> refuses
///     a database that exists and <see cref="Delete" /> one that doesn't, on every database.
/// </summary>
/// <remarks>
///     Behaviour reference (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerDatabaseCreator.cs
///     and https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Storage/Internal/SqliteDatabaseCreator.cs
/// </remarks>
internal class AdoNetDatabaseCreator(
    RelationalDatabaseCreatorDependencies dependencies,
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    IAdoNetDialect dialect,
    RelationalConnectionDependencies connectionDependencies,
    AdoNetDataSources dataSources,
    IDbContextOptions options)
    : RelationalDatabaseCreator(dependencies)
{
    // A server database can take a moment to accept connections after CREATE DATABASE returns.
    private static readonly TimeSpan CreatedTimeout = TimeSpan.FromSeconds(30);

    private DatabaseLifecycle Lifecycle
        => dialect.Sql.Databases
            ?? throw new NotSupportedException($"Creating or deleting a database is not supported by the '{dialect.Name}' ADO.NET dialect.");

    private IRelationalConnection Connection
        => Dependencies.Connection;

    public override bool Exists()
        => dialect.Sql.Databases is { Storage: not DatabaseStorage.Server } lifecycle ? ExistsOnDisk(lifecycle) : CanOpen();

    public override async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
        => dialect.Sql.Databases is { Storage: not DatabaseStorage.Server } lifecycle ? ExistsOnDisk(lifecycle) : await CanOpenAsync(cancellationToken);

    public override bool HasTables()
        => Convert.ToInt64(rawSqlCommandBuilder.Build(dialect.Sql.HasTablesSql).ExecuteScalar(Parameters(Connection)), CultureInfo.InvariantCulture)
            != 0;

    public override async Task<bool> HasTablesAsync(CancellationToken cancellationToken = default)
        => Convert.ToInt64(
                await rawSqlCommandBuilder.Build(dialect.Sql.HasTablesSql).ExecuteScalarAsync(Parameters(Connection), cancellationToken),
                CultureInfo.InvariantCulture)
            != 0;

    public override void Create()
    {
        var lifecycle = Lifecycle;
        if (lifecycle.Storage == DatabaseStorage.Server)
        {
            using var admin = AdminConnection(lifecycle, out var name);
            Execute(admin, [Statement(lifecycle.CreateSql, name)]);
            WaitUntilOpenable();
            return;
        }

        RefuseIfExists(lifecycle);
        if (lifecycle.Storage == DatabaseStorage.Directory)
        {
            System.IO.Directory.CreateDirectory(Path(lifecycle));
            return;
        }

        CreateFile(lifecycle);
        Initialize(lifecycle);
    }

    public override async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        var lifecycle = Lifecycle;
        if (lifecycle.Storage == DatabaseStorage.Server)
        {
            await using var admin = AdminConnection(lifecycle, out var name);
            await ExecuteAsync(admin, [Statement(lifecycle.CreateSql, name)], cancellationToken);
            await WaitUntilOpenableAsync(cancellationToken);
            return;
        }

        RefuseIfExists(lifecycle);
        if (lifecycle.Storage == DatabaseStorage.Directory)
        {
            System.IO.Directory.CreateDirectory(Path(lifecycle));
            return;
        }

        CreateFile(lifecycle);
        await InitializeAsync(lifecycle, cancellationToken);
    }

    public override void Delete()
    {
        var lifecycle = Lifecycle;
        ClearPool(lifecycle);
        if (lifecycle.Storage == DatabaseStorage.Server)
        {
            using var admin = AdminConnection(lifecycle, out var name);
            Execute(admin, lifecycle.DropSql.Select(sql => Statement(sql, name)).ToList());
            return;
        }

        DeleteFromDisk(lifecycle);
    }

    public override async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var lifecycle = Lifecycle;
        ClearPool(lifecycle);
        if (lifecycle.Storage == DatabaseStorage.Server)
        {
            await using var admin = AdminConnection(lifecycle, out var name);
            await ExecuteAsync(admin, lifecycle.DropSql.Select(sql => Statement(sql, name)).ToList(), cancellationToken);
            return;
        }

        DeleteFromDisk(lifecycle);
    }

    private bool CanOpen()
    {
        try
        {
            Connection.Open(errorsExpected: true);
            Connection.Close();
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private async Task<bool> CanOpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Connection.OpenAsync(cancellationToken, errorsExpected: true);
            await Connection.CloseAsync();
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private void WaitUntilOpenable()
    {
        var giveUp = DateTime.UtcNow + CreatedTimeout;
        while (!CanOpen())
        {
            if (DateTime.UtcNow > giveUp)
            {
                throw new TimeoutException($"The database was created but didn't accept a connection within {CreatedTimeout.TotalSeconds} seconds.");
            }

            Thread.Sleep(100);
        }
    }

    private async Task WaitUntilOpenableAsync(CancellationToken cancellationToken)
    {
        var giveUp = DateTime.UtcNow + CreatedTimeout;
        while (!await CanOpenAsync(cancellationToken))
        {
            if (DateTime.UtcNow > giveUp)
            {
                throw new TimeoutException($"The database was created but didn't accept a connection within {CreatedTimeout.TotalSeconds} seconds.");
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    // The connection string's own builder (from the driver's factory), so its keyword aliases are understood.
    private DbConnectionStringBuilder Builder()
    {
        var builder = Factory().CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        builder.ConnectionString = Connection.ConnectionString
            ?? throw new InvalidOperationException("Creating or deleting a database needs a connection string that names it.");
        return builder;
    }

    // The factory the context was configured with, else the one the connection names.
    private DbProviderFactory Factory()
        => options.FindExtension<Infrastructure.AdoNetOptionsExtension>()?.ProviderFactory
            ?? DbProviderFactories.GetFactory(Connection.DbConnection)
            ?? throw new InvalidOperationException("The connection's driver names no DbProviderFactory, which creating or deleting a database needs.");

    private string Path(DatabaseLifecycle lifecycle)
        => Builder().TryGetValue(lifecycle.Keyword, out var path) && (Convert.ToString(path, CultureInfo.InvariantCulture) is { Length: > 0 } text)
            ? System.IO.Path.GetFullPath(text)
            : throw new InvalidOperationException($"The connection string has no '{lifecycle.Keyword}' naming the database.");

    private bool ExistsOnDisk(DatabaseLifecycle lifecycle)
        => lifecycle.IsInMemory(Builder())
            || (lifecycle.Storage == DatabaseStorage.Directory ? System.IO.Directory.Exists(Path(lifecycle)) : File.Exists(Path(lifecycle)));

    private void RefuseIfExists(DatabaseLifecycle lifecycle)
    {
        if (ExistsOnDisk(lifecycle))
        {
            throw new InvalidOperationException($"The database '{Name(lifecycle)}' already exists.");
        }
    }

    private string Name(DatabaseLifecycle lifecycle)
        => lifecycle.IsInMemory(Builder()) ? "(in memory)" : Path(lifecycle);

    // The driver creates the file, in its own format, when a connection to a missing one opens.
    private void CreateFile(DatabaseLifecycle lifecycle)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path(lifecycle))!);
        Connection.Open();
        Connection.Close();
    }

    private void Initialize(DatabaseLifecycle lifecycle)
    {
        if (lifecycle.InitializeSql != null)
        {
            rawSqlCommandBuilder.Build(lifecycle.InitializeSql).ExecuteNonQuery(Parameters(Connection));
        }
    }

    private async Task InitializeAsync(DatabaseLifecycle lifecycle, CancellationToken cancellationToken)
    {
        if (lifecycle.InitializeSql != null)
        {
            await rawSqlCommandBuilder.Build(lifecycle.InitializeSql).ExecuteNonQueryAsync(Parameters(Connection), cancellationToken);
        }
    }

    private void DeleteFromDisk(DatabaseLifecycle lifecycle)
    {
        if (lifecycle.IsInMemory(Builder()))
        {
            return;
        }

        if (!ExistsOnDisk(lifecycle))
        {
            throw new InvalidOperationException($"The database '{Name(lifecycle)}' does not exist.");
        }

        var path = Path(lifecycle);
        if (lifecycle.Storage == DatabaseStorage.Directory)
        {
            System.IO.Directory.Delete(path, recursive: true);
            return;
        }

        File.Delete(path);
        foreach (var suffix in lifecycle.CompanionSuffixes)
        {
            File.Delete(path + suffix);
        }
    }

    // Pooled connections to the database would keep it open (a file locked, a server database in use), or, once it is
    // dropped, be handed out dead. A driver that pools per data source pools in the one cached for this connection string;
    // one that pools globally, in its own pool, which only its ClearPool reaches.
    private void ClearPool(DatabaseLifecycle lifecycle)
    {
        Connection.Close();
        lifecycle.ClearPool?.Invoke(Connection.DbConnection);
        if ((Connection.ConnectionString is { } connectionString) && dataSources.Evict(Factory(), connectionString))
        {
            // The context's connection came from that data source: let EF make a new one when it next needs it. Releasing
            // a connection also forgets its connection string, which still names the database.
            Connection.DbConnection = null!;
            Connection.ConnectionString = connectionString;
        }
    }

    private IRelationalConnection AdminConnection(DatabaseLifecycle lifecycle, out string name)
    {
        var builder = Builder();
        name = builder.TryGetValue(lifecycle.Keyword, out var value) && (Convert.ToString(value, CultureInfo.InvariantCulture) is { Length: > 0 } text)
            ? text
            : throw new InvalidOperationException($"The connection string has no '{lifecycle.Keyword}' naming the database.");
        if (lifecycle.AdminDatabase == null)
        {
            builder.Remove(lifecycle.Keyword);
        }
        else
        {
            builder[lifecycle.Keyword] = lifecycle.AdminDatabase;
        }

        var options = new DbContextOptionsBuilder()
            .UseAdoNet(Factory(), builder.ConnectionString, dialect)
            .Options;
        return new AdoNetRelationalConnection(connectionDependencies with { ContextOptions = options }, dataSources, dialect.Capabilities);
    }

    private string Statement(string template, string name)
        => template.Replace("{0}", Dependencies.SqlGenerationHelper.DelimitIdentifier(name), StringComparison.Ordinal);

    // Outside any transaction: CREATE and DROP DATABASE can't run in one on most servers.
    private void Execute(IRelationalConnection admin, IReadOnlyList<string> statements)
        => Dependencies.MigrationCommandExecutor.ExecuteNonQuery(Commands(statements), admin, new MigrationExecutionState(), commitTransaction: true);

    private Task ExecuteAsync(IRelationalConnection admin, IReadOnlyList<string> statements, CancellationToken cancellationToken)
        => Dependencies.MigrationCommandExecutor.ExecuteNonQueryAsync(
            Commands(statements), admin, new MigrationExecutionState(), commitTransaction: true, cancellationToken: cancellationToken);

    private List<MigrationCommand> Commands(IReadOnlyList<string> statements)
        => statements
            .Select(sql => new MigrationCommand(rawSqlCommandBuilder.Build(sql), Dependencies.CurrentContext.Context, Dependencies.CommandLogger, transactionSuppressed: true))
            .ToList();

    private RelationalCommandParameterObject Parameters(IRelationalConnection connection)
        => new(connection, null, null, Dependencies.CurrentContext.Context, Dependencies.CommandLogger, CommandSource.Migrations);
}

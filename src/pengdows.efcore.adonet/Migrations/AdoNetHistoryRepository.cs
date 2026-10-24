using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Migrations;

/// <summary>
///     The migrations history table in ISO SQL: existence through <c>INFORMATION_SCHEMA.TABLES</c>, and a lock that is a row in
///     a lock table, inserted to acquire (the primary key turns away a second migrator) and deleted to release. Idempotent
///     scripts need conditional SQL, which ISO SQL lacks outside procedures; a dialect that has it overrides those members.
/// </summary>
public class AdoNetHistoryRepository(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
    : HistoryRepository(dependencies), IHistoryRepository
{
    protected IAdoNetDialect Dialect { get; } = dialect;

    protected virtual string LockTableName
        => "__EFMigrationsLock";

    /// <summary>How long to wait for another migrator's lock.</summary>
    protected virtual TimeSpan LockTimeout
        => TimeSpan.FromMinutes(2);

    public override LockReleaseBehavior LockReleaseBehavior
        => LockReleaseBehavior.Explicit;

    protected override string ExistsSql
        => TableExistsSql(TableName, TableSchema);

    /// <summary>Returns a non-zero count when the table exists.</summary>
    protected virtual string TableExistsSql(string table, string? schema)
    {
        var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = "
            + Literal(table);
        return schema == null || !Dialect.Sql.SupportsSchemas ? sql : sql + " AND TABLE_SCHEMA = " + Literal(schema);
    }

    protected override bool InterpretExistsResult(object? value)
        => value != null && value != DBNull.Value && Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;

    // Migrate creates the table through these, not through the script, so they work without conditional SQL.
    bool IHistoryRepository.CreateIfNotExists()
    {
        if (Exists())
        {
            return false;
        }

        Create();
        return true;
    }

    async Task<bool> IHistoryRepository.CreateIfNotExistsAsync(CancellationToken cancellationToken)
    {
        if (await ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await CreateAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>For scripts: <c>CREATE TABLE IF NOT EXISTS</c>, which most databases accept though ISO SQL lacks it.</summary>
    public override string GetCreateIfNotExistsScript()
        => GetCreateScript().Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal);

    public override string GetBeginIfNotExistsScript(string migrationId)
        => throw IdempotentScriptsNotSupported();

    public override string GetBeginIfExistsScript(string migrationId)
        => throw IdempotentScriptsNotSupported();

    public override string GetEndIfScript()
        => throw IdempotentScriptsNotSupported();

    private NotSupportedException IdempotentScriptsNotSupported()
        => new($"Dialect '{Dialect.Name}': idempotent migration scripts need conditional SQL, which ISO SQL has no statement for.");

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        if (!TableExists(LockTableName))
        {
            CreateLockTable();
        }

        var deadline = DateTime.UtcNow + LockTimeout;
        for (var delay = TimeSpan.FromMilliseconds(50); ; delay = NextDelay(delay))
        {
            try
            {
                Command(AcquireLockSql()).ExecuteNonQuery(Parameters());
                return new Lock(this);
            }
            catch (DbException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(delay);
            }
        }
    }

    public override async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(LockTableName, cancellationToken).ConfigureAwait(false))
        {
            await CreateLockTableAsync(cancellationToken).ConfigureAwait(false);
        }

        var deadline = DateTime.UtcNow + LockTimeout;
        for (var delay = TimeSpan.FromMilliseconds(50); ; delay = NextDelay(delay))
        {
            try
            {
                await Command(AcquireLockSql()).ExecuteNonQueryAsync(Parameters(), cancellationToken).ConfigureAwait(false);
                return new Lock(this);
            }
            catch (DbException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    protected virtual string AcquireLockSql()
        => $"INSERT INTO {LockTable} ({Column("Id")}, {Column("Timestamp")}) VALUES (1, "
            + Literal(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)) + ")";

    protected virtual string ReleaseLockSql()
        => $"DELETE FROM {LockTable} WHERE {Column("Id")} = 1";

    /// <summary>The lock table, through the migrations SQL generator so its DDL is the dialect's.</summary>
    protected virtual IReadOnlyList<MigrationCommand> GetCreateLockTableCommands()
        => Dependencies.MigrationsSqlGenerator.Generate(
        [
            new CreateTableOperation
            {
                Name = LockTableName,
                Schema = TableSchema,
                Columns =
                {
                    new AddColumnOperation { Name = "Id", Table = LockTableName, Schema = TableSchema, ClrType = typeof(int) },
                    new AddColumnOperation
                    {
                        Name = "Timestamp", Table = LockTableName, Schema = TableSchema, ClrType = typeof(string), MaxLength = 64
                    }
                },
                PrimaryKey = new AddPrimaryKeyOperation
                {
                    Name = "PK_" + LockTableName, Table = LockTableName, Schema = TableSchema, Columns = ["Id"]
                }
            }
        ]);

    private void CreateLockTable()
    {
        try
        {
            Dependencies.MigrationCommandExecutor.ExecuteNonQuery(GetCreateLockTableCommands(), Dependencies.Connection);
        }
        catch (DbException) when (TableExists(LockTableName))
        {
            // Another migrator created it first.
        }
    }

    private async Task CreateLockTableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Dependencies.MigrationCommandExecutor.ExecuteNonQueryAsync(
                    GetCreateLockTableCommands(), Dependencies.Connection, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbException)
        {
            // Another migrator may have created it first.
            if (!await TableExistsAsync(LockTableName, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }
        }
    }

    private bool TableExists(string table)
        => InterpretExistsResult(Command(TableExistsSql(table, TableSchema)).ExecuteScalar(Parameters()));

    private async Task<bool> TableExistsAsync(string table, CancellationToken cancellationToken)
        => InterpretExistsResult(
            await Command(TableExistsSql(table, TableSchema)).ExecuteScalarAsync(Parameters(), cancellationToken).ConfigureAwait(false));

    private static TimeSpan NextDelay(TimeSpan delay)
        => delay < TimeSpan.FromSeconds(2) ? delay * 2 : delay;

    private string LockTable
        => SqlGenerationHelper.DelimitIdentifier(LockTableName, TableSchema);

    private string Column(string name)
        => SqlGenerationHelper.DelimitIdentifier(name);

    private static string Literal(string value)
        => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private IRelationalCommand Command(string sql)
        => Dependencies.RawSqlCommandBuilder.Build(sql);

    private RelationalCommandParameterObject Parameters()
        => new(Dependencies.Connection, null, null, Dependencies.CurrentContext.Context, Dependencies.CommandLogger, CommandSource.Migrations);

    private sealed class Lock(AdoNetHistoryRepository repository) : IMigrationsDatabaseLock
    {
        public IHistoryRepository HistoryRepository
            => repository;

        public void Dispose()
            => repository.Command(repository.ReleaseLockSql()).ExecuteNonQuery(repository.Parameters());

        public async ValueTask DisposeAsync()
            => await repository.Command(repository.ReleaseLockSql()).ExecuteNonQueryAsync(repository.Parameters()).ConfigureAwait(false);
    }
}

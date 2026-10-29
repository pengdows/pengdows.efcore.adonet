using System.Runtime.ExceptionServices;
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
    protected virtual IAdoNetDialect Dialect { get; } = dialect;

    protected virtual string LockTableName
        => "__EFMigrationsLock";

    /// <summary>How long to wait for another migrator's lock.</summary>
    protected virtual TimeSpan LockTimeout
        => TimeSpan.FromMinutes(2);

    /// <summary>
    ///     A dialect with a server-side session lock returns the statement that takes it, which yields 1 when taken; it creates
    ///     no table, and the lock is held by the connection rather than a transaction. Null for the lock table.
    /// </summary>
    protected virtual string? SessionLockSql(TimeSpan timeout)
        => null;

    /// <summary>The statement releasing the session lock, paired with <see cref="SessionLockSql" />.</summary>
    protected virtual string? SessionUnlockSql
        => null;

    public override LockReleaseBehavior LockReleaseBehavior
        => LockReleaseBehavior.Explicit;

    protected override string ExistsSql
        => TableExistsSql(TableName, TableSchema);

    /// <summary>Returns a non-zero count when the table exists.</summary>
    protected virtual string TableExistsSql(string table, string? schema)
    {
        var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = "
            + Literal(table);
        if ((schema != null) && Dialect.Sql.SupportsSchemas)
        {
            return sql + " AND TABLE_SCHEMA = " + Literal(schema);
        }

        return Dialect.Sql.CurrentSchema is { } current ? sql + " AND TABLE_SCHEMA = " + current : sql;
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

    // The lock is a row. A refused insert means another migrator holds it, unless no row is there: then, after one more try
    // (it may have been released in between), the error is real. A row older than LockTimeout is a migrator that died
    // holding it, and is taken over.
    public override IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        if (SessionLockSql(LockTimeout) is { } sessionLock)
        {
            return AcquireSessionLock(sessionLock);
        }

        if (!TableExists(LockTableName))
        {
            CreateLockTable();
        }

        var deadline = DateTime.UtcNow + LockTimeout;
        var delay = TimeSpan.FromMilliseconds(50);
        var refusals = new Refusals();
        while (true)
        {
            try
            {
                Command(AcquireLockSql()).ExecuteNonQuery(Parameters());
                return new Lock(this);
            }
            catch (DbException refused)
            {
                var holder = Holder(Command(HolderSql()).ExecuteScalar(Parameters()));
                switch (Next(holder, refused, deadline, refusals))
                {
                    case Retry.Now:
                        continue;
                    case Retry.TakeOver:
                        Command(TakeOverSql(holder!)).ExecuteNonQuery(Parameters());
                        continue;
                }

                Thread.Sleep(delay);
                delay = NextDelay(delay);
            }
        }
    }

    public override async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
    {
        if (SessionLockSql(LockTimeout) is { } sessionLock)
        {
            return await AcquireSessionLockAsync(sessionLock, cancellationToken).ConfigureAwait(false);
        }

        if (!await TableExistsAsync(LockTableName, cancellationToken).ConfigureAwait(false))
        {
            await CreateLockTableAsync(cancellationToken).ConfigureAwait(false);
        }

        var deadline = DateTime.UtcNow + LockTimeout;
        var delay = TimeSpan.FromMilliseconds(50);
        var refusals = new Refusals();
        while (true)
        {
            DbException refused;
            try
            {
                await Command(AcquireLockSql()).ExecuteNonQueryAsync(Parameters(), cancellationToken).ConfigureAwait(false);
                return new Lock(this);
            }
            catch (DbException e)
            {
                refused = e;
            }

            var holder = Holder(await Command(HolderSql()).ExecuteScalarAsync(Parameters(), cancellationToken).ConfigureAwait(false));
            switch (Next(holder, refused, deadline, refusals))
            {
                case Retry.Now:
                    continue;
                case Retry.TakeOver:
                    await Command(TakeOverSql(holder!)).ExecuteNonQueryAsync(Parameters(), cancellationToken).ConfigureAwait(false);
                    continue;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            delay = NextDelay(delay);
        }
    }

    // The session lock waits up to LockTimeout itself; it is refused only when the wait ends without it (0), or on an error
    // (NULL). Either way the migrator has not got it.
    private IMigrationsDatabaseLock AcquireSessionLock(string sql)
    {
        if (!SessionLockTaken(Command(sql).ExecuteScalar(Parameters())))
        {
            throw new TimeoutException($"Timed out after {LockTimeout} waiting for the migrations session lock.");
        }

        return new Lock(this);
    }

    private async Task<IMigrationsDatabaseLock> AcquireSessionLockAsync(string sql, CancellationToken cancellationToken)
    {
        var value = await Command(sql).ExecuteScalarAsync(Parameters(), cancellationToken).ConfigureAwait(false);
        if (!SessionLockTaken(value))
        {
            throw new TimeoutException($"Timed out after {LockTimeout} waiting for the migrations session lock.");
        }

        return new Lock(this);
    }

    private static bool SessionLockTaken(object? value)
        => value is not null and not DBNull && Convert.ToInt64(value, CultureInfo.InvariantCulture) == 1;

    private string ReleaseSql()
        => SessionUnlockSql ?? ReleaseLockSql();

    private enum Retry
    {
        Now,
        TakeOver,
        AfterDelay
    }

    private sealed class Refusals
    {
        public int Unheld { get; set; }
        public bool TookOver { get; set; }
    }

    // Each way out is bounded: a refusal with no holder is retried once, a stale holder is taken over once, and waiting
    // ends at the deadline. A transient refusal with no holder (a deadlock victim among parallel inserts of the row) is
    // waited out like a held lock.
    private Retry Next(string? holder, DbException refused, DateTime deadline, Refusals refusals)
    {
        if ((holder == null) && Dialect.Capabilities.IsTransient(refused))
        {
            if (DateTime.UtcNow >= deadline)
            {
                ExceptionDispatchInfo.Throw(refused);
            }

            return Retry.AfterDelay;
        }

        if (holder == null)
        {
            refusals.Unheld++;
            if (refusals.Unheld > 1)
            {
                ExceptionDispatchInfo.Throw(refused);
            }

            return Retry.Now;
        }

        if (DateTime.UtcNow >= deadline)
        {
            throw new TimeoutException(
                $"Timed out after {LockTimeout} waiting for the migrations lock in {LockTable}, held since {holder}.", refused);
        }

        if (!refusals.TookOver
            && DateTimeOffset.TryParse(holder, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var since)
            && (DateTimeOffset.UtcNow - since > LockTimeout))
        {
            refusals.TookOver = true;
            return Retry.TakeOver;
        }

        return Retry.AfterDelay;
    }

    private static string? Holder(object? value)
        => value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);

    protected virtual string AcquireLockSql()
        => $"INSERT INTO {LockTable} ({Column("Id")}, {Column("Timestamp")}) VALUES (1, "
            + Literal(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)) + ")";

    protected virtual string HolderSql()
        => $"SELECT {Column("Timestamp")} FROM {LockTable} WHERE {Column("Id")} = 1";

    // Only the stale holder's row: a migrator that took the lock in the meantime keeps it.
    protected virtual string TakeOverSql(string holder)
        => $"DELETE FROM {LockTable} WHERE {Column("Id")} = 1 AND {Column("Timestamp")} = {Literal(holder)}";

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
        catch (Exception error) when (error is not OperationCanceledException && TableExists(LockTableName))
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
        catch (Exception error) when (error is not OperationCanceledException)
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

    // EF's migrator begins its transaction before generating a migration's SQL and, if generating throws, releases the lock
    // before disposing that transaction: a DELETE there would be rolled back with it, leaving the lock held. The transaction
    // is doomed and the lock is the migrator's only while it owns a transaction, so it is rolled back first.
    private sealed class Lock(AdoNetHistoryRepository repository) : IMigrationsDatabaseLock
    {
        public IHistoryRepository HistoryRepository
            => repository;

        public void Dispose()
        {
            repository.Dependencies.Connection.CurrentTransaction?.Rollback();
            repository.Command(repository.ReleaseSql()).ExecuteNonQuery(repository.Parameters());
        }

        public async ValueTask DisposeAsync()
        {
            if (repository.Dependencies.Connection.CurrentTransaction is { } transaction)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
            }

            await repository.Command(repository.ReleaseSql()).ExecuteNonQueryAsync(repository.Parameters()).ConfigureAwait(false);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     The migrations lock as a SQL Server application lock held by the session. sp_getapplock waits up to the lock timeout, and
///     the session keeps the lock until it is released or its connection closes. No row is written, so no timeout can take the
///     lock from a migrator that is still running; a migrator that dies releases it when its connection goes.
/// </summary>
internal class SqlServerHistoryRepository(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetHistoryRepository(dependencies, dialect)
{
    private const string LockResource = "__EFMigrationsLock";

    // Yields 1 when the lock was granted: sp_getapplock returns a non-negative value on a grant, and -1 when the wait timed out.
    protected override string? SessionLockSql(TimeSpan timeout)
        => "DECLARE @result int; EXEC @result = sp_getapplock @Resource = N'" + LockResource
            + "', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = " + (long)timeout.TotalMilliseconds
            + "; SELECT CASE WHEN @result >= 0 THEN 1 ELSE 0 END";

    protected override string? SessionUnlockSql
        => "EXEC sp_releaseapplock @Resource = N'" + LockResource + "', @LockOwner = 'Session'";
}

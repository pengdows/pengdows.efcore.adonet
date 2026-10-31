using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     The migrations lock on SQL Server is a session-owned application lock: while a migrator holds it, another session cannot
///     take it, and it is free for the next migrator once released. No timeout takes it over from a migrator that is still
///     running.
/// </summary>
public class SqlServerMigrationLockTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
        => await fixture.ResetAsync();

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task A_held_migrations_lock_refuses_another_session_until_it_is_released()
    {
        await using var holder = fixture.CreateContext();
        holder.Database.OpenConnection();
        var history = holder.GetService<IHistoryRepository>();

        var held = history.AcquireDatabaseLock();

        Assert.True(await TryTakeAsync(timeoutMilliseconds: 0) < 0, "the lock should be refused while it is held");

        held.Dispose();

        Assert.True(await TryTakeAsync(timeoutMilliseconds: 0) >= 0, "the lock should be free once it is released");
    }

    // A probe session asks for the same resource without waiting. Its connection is closed after the answer, which releases the
    // lock it might have taken, so the probe never holds it.
    private async Task<int> TryTakeAsync(int timeoutMilliseconds)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "DECLARE @result int; EXEC @result = sp_getapplock @Resource = N'__EFMigrationsLock', @LockMode = 'Exclusive', "
            + "@LockOwner = 'Session', @LockTimeout = " + timeoutMilliseconds + "; SELECT @result;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(Ct));
    }
}

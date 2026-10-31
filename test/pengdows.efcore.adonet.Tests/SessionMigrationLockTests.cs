using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A session lock statement yields 1 when the server granted the lock. Anything else (0 for a timed-out wait, NULL) is a refusal,
///     reported with the wait that ran out.
/// </summary>
public class SessionMigrationLockTests
{
    [Theory]
    [InlineData(1L)]
    [InlineData(1)]
    [InlineData("1")]
    public void A_scalar_of_one_is_granted(object value)
        => Assert.True(SessionMigrationLock.IsGranted(value));

    [Theory]
    [InlineData(0L)]
    [InlineData(null)]
    public void Zero_or_no_value_is_refused(object? value)
        => Assert.False(SessionMigrationLock.IsGranted(value));

    [Fact]
    public void A_DBNull_is_refused()
        => Assert.False(SessionMigrationLock.IsGranted(DBNull.Value));

    [Fact]
    public void The_refusal_names_the_wait_that_ran_out()
    {
        var error = SessionMigrationLock.Refusal(TimeSpan.FromSeconds(120));

        Assert.IsType<TimeoutException>(error);
        Assert.Contains(TimeSpan.FromSeconds(120).ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains("session lock", error.Message, StringComparison.Ordinal);
    }
}

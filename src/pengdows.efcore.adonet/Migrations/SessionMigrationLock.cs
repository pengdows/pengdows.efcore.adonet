using System.Globalization;

namespace pengdows.efcore.adonet.Migrations;

/// <summary>
///     The rules of a session lock: a server-side lock that the database grants to a session and releases when the session ends or the
///     lock is released. The statements that take and release it are the dialect's (see <c>SessionLockSql</c>); this type says what the
///     answer means and what a refusal reports.
/// </summary>
internal static class SessionMigrationLock
{
    /// <summary>Whether the lock statement's answer grants the lock: it yields 1 when granted, and 0 (or no value) when the wait ran out.</summary>
    public static bool IsGranted(object? value)
        => value is not null and not DBNull && Convert.ToInt64(value, CultureInfo.InvariantCulture) == 1;

    /// <summary>The error for a refused session lock: the wait it ran out after.</summary>
    public static TimeoutException Refusal(TimeSpan timeout)
        => new($"Timed out after {timeout} waiting for the migrations session lock.");
}

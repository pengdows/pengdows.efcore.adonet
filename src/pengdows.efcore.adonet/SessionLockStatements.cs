namespace pengdows.efcore.adonet;

/// <summary>
///     A dialect's server-side session lock for migrations: the statement that takes it, which yields 1 when granted and waits up to
///     the timeout, and the statement that releases it. The lock is held by the session, so the migrator keeps its connection open
///     until it is released.
/// </summary>
public sealed class SessionLockStatements(Func<TimeSpan, string> acquire, string release)
{
    /// <summary>The statement taking the lock, waiting up to the given timeout.</summary>
    public Func<TimeSpan, string> Acquire { get; } = acquire;

    /// <summary>The statement releasing the lock.</summary>
    public string Release { get; } = release;
}

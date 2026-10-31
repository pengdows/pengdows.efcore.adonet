namespace pengdows.efcore.adonet.Migrations;

/// <summary>
///     An exclusive lock on a file, held for a migration. FileShare.None is exclusive on every platform .NET runs on: a second open of
///     the file, from this process or another, fails while this one is open. The operating system releases the lock when the holder
///     exits, however it exits. The file itself stays beside the database after release, unlocked and holding no state.
/// </summary>
internal sealed class FileMigrationLock : IDisposable, IAsyncDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);

    private readonly FileStream _file;

    private FileMigrationLock(FileStream file)
        => _file = file;

    /// <summary>The lock if the file is not held by another open, otherwise null.</summary>
    public static FileMigrationLock? TryAcquire(string path)
    {
        try
        {
            return new FileMigrationLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Waits for the lock, retrying until <paramref name="timeout" />. A network location is refused before any wait.</summary>
    public static FileMigrationLock Acquire(string path, TimeSpan timeout)
    {
        RefuseNetworkLock(path);
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (TryAcquire(path) is { } held)
            {
                return held;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out after {timeout} waiting for the migrations lock file {path}.");
            }

            Thread.Sleep(RetryDelay);
        }
    }

    /// <summary>The asynchronous form of <see cref="Acquire" />.</summary>
    public static async Task<FileMigrationLock> AcquireAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        RefuseNetworkLock(path);
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (TryAcquire(path) is { } held)
            {
                return held;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out after {timeout} waiting for the migrations lock file {path}.");
            }

            await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
        => _file.Dispose();

    public ValueTask DisposeAsync()
    {
        _file.Dispose();
        return ValueTask.CompletedTask;
    }

    // An exclusive file lock is exclusive on one host only. A lock file on a network share would let two hosts migrate at once,
    // so the migrations are refused, before any lock is taken, and the database must move to local storage or a server.
    private static void RefuseNetworkLock(string path)
    {
        var mounts = OperatingSystem.IsLinux() && File.Exists("/proc/mounts") ? File.ReadAllText("/proc/mounts") : null;
        if (NetworkPath.IsNetwork(path, mounts) || NetworkPath.IsNetworkDrive(path))
        {
            throw new InvalidOperationException(
                $"The migrations lock file {path} is on a network share, where an exclusive file lock is not exclusive across hosts. "
                + "Move the database to local storage, or use a database server, before migrating.");
        }
    }
}

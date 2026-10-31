using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     The migrations file lock on its own: exclusive while held, granted once released, timed out with the path named, and refused for
///     a network location before any wait.
/// </summary>
public class FileMigrationLockTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pengdows-filelock-{Guid.NewGuid():N}.migrations.lock");

    public void Dispose()
    {
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void A_held_file_lock_refuses_a_second_holder_and_is_granted_after_release()
    {
        using var first = FileMigrationLock.TryAcquire(_path);
        Assert.NotNull(first);

        Assert.Null(FileMigrationLock.TryAcquire(_path));

        first.Dispose();
        using var second = FileMigrationLock.TryAcquire(_path);
        Assert.NotNull(second);
    }

    [Fact]
    public void Waiting_past_the_timeout_names_the_lock_file()
    {
        using var held = FileMigrationLock.TryAcquire(_path);

        var error = Assert.Throws<TimeoutException>(() => FileMigrationLock.Acquire(_path, TimeSpan.FromMilliseconds(100)));

        Assert.Contains(_path, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_network_location_is_refused_before_any_wait()
    {
        var started = DateTime.UtcNow;

        Assert.Throws<InvalidOperationException>(() => FileMigrationLock.Acquire("//server/share/app.migrations.lock", TimeSpan.FromMinutes(2)));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task The_asynchronous_acquire_is_exclusive_too()
    {
        using var held = FileMigrationLock.TryAcquire(_path);

        await Assert.ThrowsAsync<TimeoutException>(() => FileMigrationLock.AcquireAsync(_path, TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
    }
}

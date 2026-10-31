using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A file lock is only exclusive on one host. A database file on a network share (UNC, NFS, SMB) is refused for migrations, so
///     these tests pin how a location is classified against a mount table.
/// </summary>
public class NetworkPathTests
{
    private const string Mounts = """
        /dev/sda1 / ext4 rw 0 0
        server:/export /mnt/nfs nfs4 rw 0 0
        //server/share /mnt/smb cifs rw 0 0
        tmpfs /tmp tmpfs rw 0 0
        """;

    [Theory]
    [InlineData(@"\\server\share\app.db")]
    [InlineData("//server/share/app.db")]
    public void A_UNC_path_is_a_network_location(string path)
        => Assert.True(NetworkPath.IsNetwork(path, mounts: null));

    [Fact]
    public void A_path_on_an_NFS_mount_is_a_network_location()
        => Assert.True(NetworkPath.IsNetwork("/mnt/nfs/app/app.db", Mounts));

    [Fact]
    public void A_path_on_an_SMB_mount_is_a_network_location()
        => Assert.True(NetworkPath.IsNetwork("/mnt/smb/app.db", Mounts));

    [Fact]
    public void A_path_on_the_local_disk_is_not_a_network_location()
        => Assert.False(NetworkPath.IsNetwork("/home/app/app.db", Mounts));

    [Fact]
    public void The_deepest_mount_decides_so_a_local_directory_under_a_network_mount_is_classified_by_its_own_mount()
    {
        const string nested = """
            server:/export /mnt/nfs nfs4 rw 0 0
            /dev/sdb1 /mnt/nfs/local ext4 rw 0 0
            """;

        Assert.False(NetworkPath.IsNetwork("/mnt/nfs/local/app.db", nested));
    }
}

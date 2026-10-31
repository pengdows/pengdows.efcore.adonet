namespace pengdows.efcore.adonet.Migrations;

/// <summary>
///     Whether a file lives on a network share, where an exclusive file lock is not exclusive across hosts. A UNC path is one; on
///     Linux a path is one when the deepest mount that holds it is a network filesystem, read from the mount table.
/// </summary>
internal static class NetworkPath
{
    private static readonly HashSet<string> NetworkFileSystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "nfs", "nfs4", "cifs", "smb", "smb2", "smb3", "smbfs", "afs", "ceph", "glusterfs", "fuse.sshfs", "9p"
    };

    /// <param name="path">The lock file's path.</param>
    /// <param name="mounts">The mount table's text (Linux's <c>/proc/mounts</c>); null when there is none to read.</param>
    public static bool IsNetwork(string path, string? mounts)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        if (mounts is null)
        {
            return false;
        }

        var full = Path.GetFullPath(path);
        string? deepestType = null;
        var deepest = -1;
        foreach (var line in mounts.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 3)
            {
                continue;
            }

            var point = fields[1].TrimEnd('/');
            var holds = point.Length == 0 || full.Equals(point, StringComparison.Ordinal) || full.StartsWith(point + "/", StringComparison.Ordinal);
            if (holds && point.Length > deepest)
            {
                deepest = point.Length;
                deepestType = fields[2];
            }
        }

        return deepestType is not null && NetworkFileSystems.Contains(deepestType);
    }
}

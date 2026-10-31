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

    /// <summary>
    ///     On Windows, whether the path's drive is a mapped network drive. Read from the drive, so it is not unit-tested on other systems.
    /// </summary>
    public static bool IsNetworkDrive(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return root is not null && new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

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

            var point = Unescape(fields[1]).TrimEnd('/');
            var holds = point.Length == 0 || full.Equals(point, StringComparison.Ordinal) || full.StartsWith(point + "/", StringComparison.Ordinal);
            if (holds && point.Length > deepest)
            {
                deepest = point.Length;
                deepestType = fields[2];
            }
        }

        return deepestType is not null && NetworkFileSystems.Contains(deepestType);
    }

    // /proc/mounts writes a space, tab, newline or backslash in a field as its three-digit octal code.
    private static string Unescape(string field)
    {
        if (!field.Contains('\\', StringComparison.Ordinal))
        {
            return field;
        }

        var builder = new System.Text.StringBuilder(field.Length);
        for (var i = 0; i < field.Length; i++)
        {
            if (field[i] == '\\' && i + 3 < field.Length
                && field.AsSpan(i + 1, 3) is var octal && octal.IndexOfAnyExcept("01234567") < 0)
            {
                builder.Append((char)Convert.ToInt32(octal.ToString(), 8));
                i += 3;
            }
            else
            {
                builder.Append(field[i]);
            }
        }

        return builder.ToString();
    }
}

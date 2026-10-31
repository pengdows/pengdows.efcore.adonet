using System.Reflection;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     The history repository's protected lock members are the file-lock path and the timeout. A dialect supplies its session lock as data
///     (<c>ISqlDialect.SessionLock</c>), so the repository has no session-lock hook to override.
/// </summary>
public class HistoryRepositorySurfaceTests
{
    private static readonly string[] AllowedProtectedLockMembers = ["FileLockPath", "LockTimeout"];

    [Fact]
    public void The_protected_lock_members_are_only_the_plugin_hooks()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var type = typeof(AdoNetHistoryRepository);
        var protectedMethods = type.GetMethods(flags).Where(m => !m.IsSpecialName && (m.IsFamily || m.IsFamilyOrAssembly)).Select(m => m.Name);
        var protectedProperties = type.GetProperties(flags)
            .Where(p => p.GetMethod is { } get && (get.IsFamily || get.IsFamilyOrAssembly))
            .Select(p => p.Name);

        var protectedLockMembers = protectedMethods.Concat(protectedProperties)
            .Where(n => n.Contains("lock", StringComparison.OrdinalIgnoreCase) || n.Contains("holder", StringComparison.OrdinalIgnoreCase)
                || n.Contains("takeover", StringComparison.OrdinalIgnoreCase) || n.Contains("release", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(AllowedProtectedLockMembers.OrderBy(n => n, StringComparer.Ordinal), protectedLockMembers);
    }
}

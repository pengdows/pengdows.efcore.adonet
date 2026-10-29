namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     REVIEW_POLICY.md section D: every public member is declared in its project's public API baseline with a one-line
///     reason. The analyzer's baseline format takes no comments, so each project keeps <c>PublicAPI.Reasons.txt</c> beside its
///     <c>PublicAPI.Unshipped.txt</c>: one line per block of declarations, keyed by the block's first declaration. Checked
///     against every unshipped baseline in the repository.
/// </summary>
public class PublicApiReasonTests
{
    public static IEnumerable<object[]> UnshippedBaselines()
        => Directory.GetFiles(RepositoryRoot() + "/src", "PublicAPI.Unshipped.txt", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/", StringComparison.Ordinal) && !f.Contains("/bin/", StringComparison.Ordinal))
            .Select(f => new object[] { Path.GetRelativePath(RepositoryRoot(), f) });

    [Theory]
    [MemberData(nameof(UnshippedBaselines))]
    public void Every_declaration_block_has_a_reason(string baseline)
    {
        var reasons = Path.Combine(RepositoryRoot(), Path.GetDirectoryName(baseline)!, "PublicAPI.Reasons.txt");
        var keyed = File.Exists(reasons)
            ? File.ReadAllLines(reasons).Where(l => !l.StartsWith('#')).Select(l => l.Split(" | ")[0]).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var undeclared = new List<string>();
        var previousWasDeclaration = false;
        foreach (var line in File.ReadAllLines(Path.Combine(RepositoryRoot(), baseline)))
        {
            var isDeclaration = !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#');
            if (isDeclaration && !previousWasDeclaration && !keyed.Contains(line))
            {
                undeclared.Add(line);
            }

            previousWasDeclaration = isDeclaration;
        }

        Assert.Empty(undeclared.Take(3));
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "REVIEW_POLICY.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("REVIEW_POLICY.md not found above the test output.");
    }
}

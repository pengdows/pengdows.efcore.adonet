namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A core source file that grows past a few hundred lines is doing more than one job. The limit keeps each file to one concern; the
///     split is by partial class, so behaviour does not change.
/// </summary>
public class SourceSizeTests
{
    private const int MaxLines = 600;

    [Fact]
    public void No_core_source_file_exceeds_the_line_limit()
    {
        var core = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/pengdows.efcore.adonet"));
        var over = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (File: Path.GetRelativePath(core, f), Lines: File.ReadLines(f).Count()))
            .Where(f => f.Lines > MaxLines)
            .Select(f => $"{f.File}: {f.Lines}")
            .ToList();

        Assert.True(Directory.Exists(core), core);
        Assert.Empty(over);
    }
}

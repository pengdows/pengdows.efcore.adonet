using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Failing parity probes for MySQL string translations currently present in the upstream provider inventory but not
///     covered by the pengdows fakeDb lane yet.
/// </summary>
public sealed class MySqlUpstreamGapFakeDbTests
{
    // Closed: MySQL's TRIM(BOTH remstr FROM str) strips remstr as one literal substring, not a character set, so the
    // gap this probe used to document (asserting that wrong shape) was a correctness bug, not a missing feature.
    // REGEXP_REPLACE strips a maximal leading/trailing run of any character in a class, which is what Trim(char[])
    // actually means.
    [Fact]
    public void Trim_character_array_translates_to_a_regex_character_class()
    {
        var db = new FakeMySql();
        using var context = db.Context<RowContext>(options => new RowContext(options));

        var sql = context.Rows.Where(row => row.Name.Trim('x', 'y') == "a").ToQueryString();

        Assert.Contains("REGEXP_REPLACE(`r`.`Name`, CONCAT('^[', 'xy', ']+|[', 'xy', ']+$'), '')", sql);
    }

    [Fact]
    public void Pad_left_and_right_translate_to_lpad_and_rpad()
    {
        var db = new FakeMySql();
        using var context = db.Context<RowContext>(options => new RowContext(options));

        var sql = context.Rows.Select(row => new { Left = row.Name.PadLeft(8, '0'), Right = row.Name.PadRight(8, '0') }).ToQueryString();

        Assert.Contains("LPAD", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RPAD", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Full_text_match_executes_through_fake_db()
    {
        var db = new FakeMySql();
        await using var context = db.Context<RowContext>(options => new RowContext(options));
        db.Returns([("Id", 7)]);

        var ids = await context.Rows
            .Where(row => EF.Functions.Match(row.Name, "cast", MySqlMatchSearchMode.NaturalLanguage) > 0)
            .Select(row => row.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([7], ids);
        Assert.Contains("MATCH", Assert.Single(db.Commands), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class RowContext(DbContextOptions<RowContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }
}

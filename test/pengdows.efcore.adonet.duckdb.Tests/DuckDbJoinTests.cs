using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>
///     A right join keeps the right side's unmatched rows, and a filter on the left side doesn't drop them. Translated as
///     RIGHT JOIN with the left filter after it, DuckDB drops them; the translation writes the same join as LEFT JOIN.
/// </summary>
public class DuckDbJoinTests(DuckDbFixture fixture) : IClassFixture<DuckDbFixture>
{
    [Fact]
    public async Task Right_join_keeps_the_unmatched_right_rows_past_a_left_side_filter()
    {
        var ct = TestContext.Current.CancellationToken;
        var batch = Guid.NewGuid().ToString("N");
        await using (var seed = fixture.CreateContext())
        {
            foreach (var rating in new[] { 1m, 2m, 3m })
            {
                seed.Add(new Blog { Name = batch + rating, IsActive = true, Rating = rating, CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() });
            }

            await seed.SaveChangesAsync(ct);
        }

        await using var context = fixture.CreateContext();
        var actual = await context.Blogs
            .Where(b => b.Name.StartsWith(batch) && b.Rating <= 2)
            .RightJoin(
                context.Blogs.Where(b => b.Name.StartsWith(batch) && b.Rating >= 2),
                outer => outer.Rating,
                inner => inner.Rating,
                (outer, inner) => new { Left = outer == null ? (decimal?)null : outer.Rating, Right = inner.Rating })
            .ToListAsync(ct);

        Assert.Equal(
            new[] { (2m, 2m), ((decimal?)null, 3m) },
            actual.OrderBy(r => r.Right).Select(r => (r.Left, r.Right)).ToArray());
    }
}

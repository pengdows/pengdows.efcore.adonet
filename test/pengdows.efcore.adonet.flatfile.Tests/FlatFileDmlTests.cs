using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.flatfile.Tests;

public class FlatFileDmlTests(FlatFileFixture fixture) : IClassFixture<FlatFileFixture>
{
    [Fact]
    public async Task Update_through_a_join_is_rejected_with_a_clear_reason()
    {
        await using var context = fixture.CreateContext();

        var error = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Posts.Where(p => p.Blog.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Title, "x"), TestContext.Current.CancellationToken));

        Assert.Contains("target alias", error.Message);
    }
}

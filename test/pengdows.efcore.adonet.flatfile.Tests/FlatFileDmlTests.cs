using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.flatfile.Tests;

public class FlatFileDmlTests(FlatFileFixture fixture) : IClassFixture<FlatFileFixture>
{
    [Fact]
    public async Task SaveChanges_batches_sequence_key_inserts_with_the_statement_separator()
    {
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<BlogContext>(fixture.Options)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted])
            .Options;

        await using var context = new BlogContext(options);
        context.AddRange(
            new Blog { Name = "batched-a", IsActive = true },
            new Blog { Name = "batched-b", IsActive = true });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var insert = Assert.Single(commands, command => command.Contains("INSERT INTO"));
        Assert.Equal(2, insert.Split("INSERT INTO").Length - 1);
        Assert.Contains(";\n", insert);
    }
}

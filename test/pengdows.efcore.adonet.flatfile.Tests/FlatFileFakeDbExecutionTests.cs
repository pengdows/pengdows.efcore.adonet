using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.flatfile.Tests;

/// <summary>Flatfile's EF execution path can be tested without touching the filesystem.</summary>
public sealed class FlatFileFakeDbExecutionTests
{
    [Fact]
    public async Task Flatfile_queries_materialize_scripted_rows_through_fakeDb()
    {
        var database = new FlatFileFakeDatabase();
        await using var context = database.Context();
        database.Returns(new Dictionary<string, object?> { ["Id"] = 3, ["Name"] = "flat" });

        var row = await context.Rows.Where(item => item.Id > 0).SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal((3, "flat"), (row.Id, row.Name));
        Assert.Contains("SELECT", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FlatFileFakeDatabase
    {
        private readonly List<string> commands = [];

        public fakeDbConnection Connection { get; } = new() { ConnectionString = "Data Source=fake;EmulatedProduct=Unknown" };
        public IReadOnlyList<string> Commands => commands;

        public FlatFileContext Context()
            => new(new DbContextOptionsBuilder<FlatFileContext>()
                .UseAdoNet(Connection, contextOwnsConnection: false, options => options.UseDialect(FlatFileDialect.Instance))
                .LogTo(message =>
                {
                    var lines = message.ReplaceLineEndings("\n").Split('\n');
                    if (lines.Length > 2)
                        commands.Add(string.Join("\n", lines.Skip(2).Select(line => line.Length >= 6 ? line[6..] : line.TrimStart())).TrimEnd());
                }, [RelationalEventId.CommandExecuted])
                .Options);

        public void Returns(params Dictionary<string, object?>[] rows)
            => Connection.EnqueueReaderResult(rows);
    }

    private sealed class FlatFileContext(DbContextOptions<FlatFileContext> options) : DbContext(options)
    {
        public DbSet<FlatFileRow> Rows => Set<FlatFileRow>();
    }

    private sealed class FlatFileRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}

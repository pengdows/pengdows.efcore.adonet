using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.Tests;

/// <summary>EF Core's SaveChanges pipeline callbacks and state transitions with fakeDb supplying the result row.</summary>
public sealed class FakeDbEfCoreSaveChangesPipelineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_interceptors_observe_saving_and_saved_with_the_generated_value_materialized()
    {
        var database = new FakeDatabase();
        var interceptor = new SaveChangesInterceptorProbe();
        await using var context = new DbContextWithProbe(
            new DbContextOptionsBuilder<DbContextWithProbe>()
                .UseAdoNet(database.Connection, contextOwnsConnection: false, o => o.UseDialect(TestDialects.Ansi))
                .AllowManyDialects()
                .AddInterceptors(interceptor)
                .Options);
        database.Returns(FakeDatabase.Row("Id", 19));

        var item = new PipelineItem { Name = "created" };
        context.Items.Add(item);
        var written = await context.SaveChangesAsync(Ct);

        Assert.Equal(1, written);
        Assert.Equal(19, item.Id);
        Assert.Equal(["SavingChanges", "SavedChanges:1"], interceptor.Events);
        Assert.Equal(EntityState.Unchanged, context.Entry(item).State);
    }

    [Fact]
    public async Task SaveChanges_interceptor_observes_failed_commands_without_reporting_saved()
    {
        var database = new FakeDatabase();
        var interceptor = new SaveChangesInterceptorProbe();
        await using var context = new DbContextWithProbe(
            new DbContextOptionsBuilder<DbContextWithProbe>()
                .UseAdoNet(database.Connection, contextOwnsConnection: false, o => o.UseDialect(TestDialects.Ansi))
                .AllowManyDialects()
                .AddInterceptors(interceptor)
                .Options);
        database.Connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object?>>());

        var item = new PipelineItem { Name = "created" };
        context.Items.Add(item);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));

        // EF raises SaveChangesFailed for command failures, but not for this post-command concurrency result.
        // It must still never report SavedChanges after the zero-row result.
        Assert.Equal(["SavingChanges"], interceptor.Events);
        Assert.Equal(EntityState.Added, context.Entry(item).State);
    }

    private sealed class DbContextWithProbe(DbContextOptions<DbContextWithProbe> options) : DbContext(options)
    {
        public DbSet<PipelineItem> Items => Set<PipelineItem>();
    }

    private sealed class PipelineItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class SaveChangesInterceptorProbe : SaveChangesInterceptor
    {
        public List<string> Events { get; } = [];

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            Events.Add("SavingChanges");
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Events.Add("SavingChanges");
            return ValueTask.FromResult(result);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Events.Add($"SavedChanges:{result}");
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            Events.Add($"SavedChanges:{result}");
            return ValueTask.FromResult(result);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
            => Events.Add($"SaveChangesFailed:{eventData.Exception.GetType().Name}");

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Events.Add($"SaveChangesFailed:{eventData.Exception.GetType().Name}");
            return Task.CompletedTask;
        }
    }
}

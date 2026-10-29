using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Additional relational EF Core surfaces executed with fakeDb supplying the server result.</summary>
public sealed class FakeDbEfCoreRelationalFeatureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Keyless_view_entities_materialize_from_a_fakeDb_reader()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<KeylessContext>(AdoNetDialect.Ansi, options => new KeylessContext(options));
        database.Returns(new Dictionary<string, object?> { ["Id"] = 2, ["Label"] = "report" });

        var report = await context.Reports.SingleAsync(Ct);

        Assert.Equal((2, "report"), (report.Id, report.Label));
        Assert.Contains("Reports", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Global_query_filters_are_sent_to_fakeDb_and_filtered_rows_materialize()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<FilteredContext>(AdoNetDialect.Ansi, options => new FilteredContext(options));
        database.Returns(new Dictionary<string, object?> { ["Id"] = 3, ["IsDeleted"] = false, ["Name"] = "live" });

        var item = await context.Items.SingleAsync(Ct);

        Assert.Equal("live", item.Name);
        Assert.Contains("IsDeleted", Assert.Single(database.Commands));
    }

    [Fact]
    public async Task Shadow_properties_can_be_projected_through_the_fakeDb_pipeline()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ShadowContext>(AdoNetDialect.Ansi, options => new ShadowContext(options));
        database.Returns(FakeDatabase.Row("Tag", "shadow"));

        var tag = await context.Items.Select(item => EF.Property<string>(item, "Tag")).SingleAsync(Ct);

        Assert.Equal("shadow", tag);
        Assert.Contains("Tag", Assert.Single(database.Commands));
    }

    [Fact]
    public async Task Composed_from_sql_queries_execute_the_composed_command_on_fakeDb()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ComposedSqlContext>(AdoNetDialect.Ansi, options => new ComposedSqlContext(options));
        database.Returns(new Dictionary<string, object?> { ["Id"] = 12, ["Name"] = "composed" });

        var item = await context.Items.FromSqlRaw("SELECT \"Id\", \"Name\" FROM \"Items\"")
            .Where(item => item.Id > 10)
            .SingleAsync(Ct);

        Assert.Equal((12, "composed"), (item.Id, item.Name));
        Assert.Contains("FROM (", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Interpolated_database_commands_are_parameterized_and_return_fakeDb_row_counts()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ComposedSqlContext>(AdoNetDialect.Ansi, options => new ComposedSqlContext(options));
        database.Connection.EnqueueNonQueryResult(4);

        var name = "updated";
        var affected = await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Items\" SET \"Name\" = {name}", Ct);

        Assert.Equal(4, affected);
        Assert.Contains("UPDATE", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@p", Assert.Single(database.Parameters), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Async_streaming_consumes_all_fakeDb_rows_without_buffering()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<StreamingContext>(AdoNetDialect.Ansi, options => new StreamingContext(options));
        database.Returns(
            new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "one" },
            new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "two" });

        var names = new List<string>();
        await foreach (var item in context.Items.AsAsyncEnumerable().WithCancellation(Ct))
            names.Add(item.Name);

        Assert.Equal(["one", "two"], names);
    }

    [Fact]
    public async Task Command_interceptors_observe_fakeDb_reader_execution()
    {
        var database = new FakeDatabase();
        var interceptor = new ReaderInterceptor();
        var options = new DbContextOptionsBuilder<InterceptedContext>()
            .UseAdoNet(database.Connection, contextOwnsConnection: false, o => o.UseDialect(AdoNetDialect.Ansi))
            .AllowManyDialects()
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new InterceptedContext(options);
        database.Returns(FakeDatabase.Row("Id", 17));

        var item = await context.Items.SingleAsync(Ct);

        Assert.Equal(17, item.Id);
        Assert.Equal(1, interceptor.ReaderExecutions);
    }

    private sealed class KeylessContext(DbContextOptions<KeylessContext> options) : DbContext(options)
    {
        public DbSet<ReportRow> Reports => Set<ReportRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ReportRow>().HasNoKey().ToView("Reports");
    }

    private sealed class ReportRow
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
    }

    private sealed class FilteredContext(DbContextOptions<FilteredContext> options) : DbContext(options)
    {
        public DbSet<FilteredItem> Items => Set<FilteredItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<FilteredItem>().HasQueryFilter(item => !item.IsDeleted);
    }

    private sealed class FilteredItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsDeleted { get; set; }
    }

    private sealed class ShadowContext(DbContextOptions<ShadowContext> options) : DbContext(options)
    {
        public DbSet<ShadowItem> Items => Set<ShadowItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowItem>().Property<string>("Tag");
    }

    private sealed class ShadowItem
    {
        public int Id { get; set; }
    }

    private sealed class ComposedSqlContext(DbContextOptions<ComposedSqlContext> options) : DbContext(options)
    {
        public DbSet<ComposedSqlItem> Items => Set<ComposedSqlItem>();
    }

    private sealed class ComposedSqlItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class StreamingContext(DbContextOptions<StreamingContext> options) : DbContext(options)
    {
        public DbSet<StreamingItem> Items => Set<StreamingItem>();
    }

    private sealed class StreamingItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class InterceptedContext(DbContextOptions<InterceptedContext> options) : DbContext(options)
    {
        public DbSet<InterceptedItem> Items => Set<InterceptedItem>();
    }

    private sealed class InterceptedItem
    {
        public int Id { get; set; }
    }

    private sealed class ReaderInterceptor : DbCommandInterceptor
    {
        public int ReaderExecutions { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ReaderExecutions++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderExecutions++;
            return ValueTask.FromResult(result);
        }
    }
}

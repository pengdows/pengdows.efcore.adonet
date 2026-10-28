using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     Whether a SQL Server connection holds several open readers is its connection string's MultipleActiveResultSets, not a
///     fact of the database: a split query streams its collections where it does and buffers them where it doesn't, and a
///     query compiled for one kind of connection isn't reused on the other. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerCompiledQueryCacheKeyGenerator.cs
///     Provider-only: MultipleActiveResultSets is SqlClient's.
/// </summary>
public class SqlServerMultipleActiveReadersTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
        => await fixture.ResetAsync();

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task A_split_query_streams_only_on_a_connection_with_multiple_active_result_sets()
    {
        await SeedAsync();

        Assert.Equal(2, await MostReadersOpenAtOnceAsync(multipleActiveResultSets: true));
        Assert.Equal(1, await MostReadersOpenAtOnceAsync(multipleActiveResultSets: false));
        Assert.Equal(2, await MostReadersOpenAtOnceAsync(multipleActiveResultSets: true));
    }

    [Fact]
    public async Task A_query_can_run_while_another_is_read_with_multiple_active_result_sets()
    {
        await SeedAsync();
        await using var context = CreateContext(multipleActiveResultSets: true);
        var counts = new List<int>();

        await foreach (var blog in context.Blogs.OrderBy(b => b.Name).AsAsyncEnumerable().WithCancellation(Ct))
        {
            counts.Add(await context.Posts.CountAsync(p => p.BlogId == blog.Id, Ct));
        }

        Assert.Equal([2, 2], counts);
    }

    private async Task<int> MostReadersOpenAtOnceAsync(bool multipleActiveResultSets)
    {
        var readers = new OpenReaders();
        await using var context = CreateContext(multipleActiveResultSets, readers);
        var counts = new List<int>();

        await foreach (var blog in context.Blogs.Include(b => b.Posts).AsSplitQuery().OrderBy(b => b.Name).AsAsyncEnumerable().WithCancellation(Ct))
        {
            counts.Add(blog.Posts.Count);
        }

        Assert.Equal([2, 2], counts);
        return readers.Most;
    }

    private BlogContext CreateContext(bool multipleActiveResultSets, OpenReaders? readers = null)
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString) { MultipleActiveResultSets = multipleActiveResultSets }
            .ConnectionString;
        var options = new DbContextOptionsBuilder<BlogContext>().UseAdoNetSqlServer(connectionString);
        if (readers != null)
        {
            options.AddInterceptors(readers);
        }

        return new BlogContext(options.Options);
    }

    // Counts the driver's readers still open as each command starts, itself included.
    private sealed class OpenReaders : DbCommandInterceptor
    {
        private readonly List<DbDataReader> _readers = [];

        public int Most { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Most = Math.Max(Most, _readers.Count(r => !r.IsClosed) + 1);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            _readers.Add(result);
            return ValueTask.FromResult(result);
        }
    }

    private async Task SeedAsync()
    {
        await using var context = fixture.CreateContext();
        foreach (var name in new[] { "a", "b" })
        {
            var blog = new Blog { Name = name, CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() };
            blog.Posts.Add(new Post { Title = name + "1" });
            blog.Posts.Add(new Post { Title = name + "2" });
            context.Add(blog);
        }

        await context.SaveChangesAsync(Ct);
    }
}

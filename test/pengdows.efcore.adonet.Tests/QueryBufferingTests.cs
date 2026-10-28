using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A split query runs one command per collection while the previous reader is still open; on a connection that holds one
///     open reader at a time (most drivers, and SQL Server without MARS) each result is buffered first, as EF's own server
///     providers do. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerQueryCompilationContext.cs
/// </summary>
public class QueryBufferingTests
{
    private static bool IsBuffering(bool multipleActiveReaders, QuerySplittingBehavior splitting)
    {
        var dialect = new PartsDialect($"Readers {multipleActiveReaders}", capabilities: new DatabaseCapabilities { SupportsMultipleActiveReaders = multipleActiveReaders });
        using var context = new FakeDatabase().Context(dialect, o => o.UseQuerySplittingBehavior(splitting));
        return context.GetService<IQueryCompilationContextFactory>().Create(async: false).IsBuffering;
    }

    [Fact]
    public void Split_queries_are_buffered_where_a_connection_holds_one_open_reader()
        => Assert.True(IsBuffering(multipleActiveReaders: false, QuerySplittingBehavior.SplitQuery));

    [Fact]
    public void Split_queries_stream_where_a_connection_holds_several_open_readers()
        => Assert.False(IsBuffering(multipleActiveReaders: true, QuerySplittingBehavior.SplitQuery));

    [Fact]
    public void Single_queries_stream_either_way()
        => Assert.False(IsBuffering(multipleActiveReaders: false, QuerySplittingBehavior.SingleQuery));

    private static readonly PartsDialect ReadersByConnectionString = new("Readers by connection string", capabilities: new ChoosingCapabilities());

    private static ItemContext ContextWith(string connectionString)
    {
        var database = new FakeDatabase();
        database.Connection.ConnectionString = connectionString;
        return database.Context(ReadersByConnectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    }

    [Fact]
    public void Split_queries_follow_the_connection_string_where_the_driver_lets_it_choose()
    {
        using var many = ContextWith("Data Source=fake;Readers=Many");
        using var one = ContextWith("Data Source=fake");

        Assert.False(many.GetService<IQueryCompilationContextFactory>().Create(async: false).IsBuffering);
        Assert.True(one.GetService<IQueryCompilationContextFactory>().Create(async: false).IsBuffering);
    }

    [Fact]
    public void A_query_compiled_for_one_reader_answer_is_not_reused_for_the_other()
    {
        using var many = ContextWith("Data Source=fake;Readers=Many");
        using var alsoMany = ContextWith("Data Source=other;Readers=Many");
        using var one = ContextWith("Data Source=fake");
        object Key(ItemContext context)
            => context.GetService<ICompiledQueryCacheKeyGenerator>().GenerateCacheKey(context.Items.Where(i => i.Id > 1).Expression, async: false);

        Assert.Equal(Key(many), Key(alsoMany));
        Assert.NotEqual(Key(many), Key(one));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MultipleActiveResultSets=False;Readers=Many;';DROP TABLE x;--")]
    [InlineData("Data Source=\u0000\uFFFF;Mars=yes")]
    public void Capabilities_answer_the_same_for_every_connection_string_unless_a_driver_chooses(string? connectionString)
    {
        Assert.True(new DatabaseCapabilities { SupportsMultipleActiveReaders = true }.SupportsMultipleActiveReadersFor(connectionString));
        Assert.False(DatabaseCapabilities.Default.SupportsMultipleActiveReadersFor(connectionString));
        Assert.False(DatabaseCapabilities.Default.SupportsMultipleActiveReadersFor(new string('x', 65536)));
    }

    private sealed record ChoosingCapabilities : DatabaseCapabilities
    {
        public override bool SupportsMultipleActiveReadersFor(string? connectionString)
            => connectionString?.Contains("Readers=Many", StringComparison.Ordinal) == true;
    }
}

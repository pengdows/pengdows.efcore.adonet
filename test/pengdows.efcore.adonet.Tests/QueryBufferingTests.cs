using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

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
}

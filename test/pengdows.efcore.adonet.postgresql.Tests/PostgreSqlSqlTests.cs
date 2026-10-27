using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.Bulk;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

public class PostgreSqlSqlTests
{
    private sealed class TemporalRow
    {
        public int Id { get; set; }
        public DateTimeOffset Value { get; set; }
        public TimeSpan Span { get; set; }
    }

    private sealed class TemporalContext(DbContextOptions<TemporalContext> options) : DbContext(options)
    {
        public DbSet<TemporalRow> Rows => Set<TemporalRow>();
    }

    private static BlogContext Context(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        configure(builder);
        return new BlogContext(builder.Options);
    }

    private static string PagedQuery(BlogContext context)
    {
        var name = "x";
        return context.Blogs.Where(b => b.Name == name).OrderBy(b => b.Id).Skip(5).Take(10).ToQueryString();
    }

    [Fact]
    public void Uses_limit_offset()
    {
        using var context = Context(b => b.UseAdoNetPostgreSql("Host=localhost"));

        var sql = PagedQuery(context);

        Assert.Contains("= @name", sql);
        Assert.Matches(@"LIMIT @p\w*\s+OFFSET @p\w*", sql);
    }

    [Fact]
    public void Bulk_loads_use_the_native_loader()
    {
        using var context = Context(b => b.UseAdoNetPostgreSql("Host=localhost"));

        Assert.IsType<PostgreSqlBulkLoader>(context.GetService<IAdoNetBulkLoader>());
    }

    [Fact]
    public void Temporal_arithmetic_uses_postgresql_intervals()
    {
        var options = new DbContextOptionsBuilder<TemporalContext>()
            .UseAdoNetPostgreSql("Host=localhost")
            .Options;
        using var context = new TemporalContext(options);

        var sql = context.Rows.Where(r => r.Value.AddSeconds(1) > r.Value && r.Span.Milliseconds == 1).ToQueryString();
        Assert.Contains("INTERVAL '1 microsecond'", sql);

        Assert.Contains("INTERVAL '1 microsecond'", sql);
        Assert.Contains("EXTRACT(MILLISECONDS", sql);
    }
}

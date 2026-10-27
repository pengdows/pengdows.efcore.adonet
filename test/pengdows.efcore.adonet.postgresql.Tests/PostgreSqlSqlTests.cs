using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.Bulk;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

public class PostgreSqlSqlTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TemporalRow>().ToTable("ProviderReviewTemporalRows");
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

    [Fact]
    public async Task DateTimeOffset_parameters_round_trip_as_the_same_utc_instant()
    {
        await fixture.CreateTablesOnceAsync<TemporalContext>(o => new(o));
        var value = new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.FromHours(2));

        await using (var context = fixture.CreateContext<TemporalContext>(o => new(o)))
        {
            context.Rows.Add(new TemporalRow { Value = value });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = fixture.CreateContext<TemporalContext>(o => new(o)))
        {
            var stored = await context.Rows.Select(row => row.Value).SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(value.UtcDateTime, stored.UtcDateTime);
            Assert.Equal(TimeSpan.Zero, stored.Offset);
        }

        await fixture.ExecuteAsync("DELETE FROM \"ProviderReviewTemporalRows\"");
    }
}

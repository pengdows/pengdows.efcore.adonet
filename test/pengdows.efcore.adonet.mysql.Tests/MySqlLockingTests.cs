using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>SQL shape of each locking read: the clause lands after the query it tags, whatever that query is.</summary>
public class MySqlLockingTests
{
    private static BlogContext Context()
        => new(new DbContextOptionsBuilder<BlogContext>().AllowManyDialects().UseAdoNetMySql("Server=localhost").Options);

    [Fact]
    public void For_update_appends_for_update()
    {
        using var context = Context();

        var sql = context.Blogs.Where(b => b.IsActive).ForUpdate().ToQueryString();

        Assert.EndsWith("FOR UPDATE", sql.TrimEnd());
    }

    [Fact]
    public void For_share_appends_for_share()
    {
        using var context = Context();

        var sql = context.Blogs.ForShare().ToQueryString();

        Assert.EndsWith("FOR SHARE", sql.TrimEnd());
    }

    [Fact]
    public void For_update_no_wait_appends_for_update_nowait()
    {
        using var context = Context();

        var sql = context.Blogs.ForUpdateNoWait().ToQueryString();

        Assert.EndsWith("FOR UPDATE NOWAIT", sql.TrimEnd());
    }

    [Fact]
    public void For_update_skip_locked_appends_for_update_skip_locked()
    {
        using var context = Context();

        var sql = context.Blogs.ForUpdateSkipLocked().ToQueryString();

        Assert.EndsWith("FOR UPDATE SKIP LOCKED", sql.TrimEnd());
    }

    [Fact]
    public void An_untagged_query_has_no_lock_clause()
    {
        using var context = Context();

        var sql = context.Blogs.ToQueryString();

        Assert.DoesNotContain("FOR UPDATE", sql);
        Assert.DoesNotContain("FOR SHARE", sql);
    }
}

/// <summary>
///     The locks are real: a second connection blocks, fails immediately, or skips the row, matching what the clause says,
///     against a real server.
/// </summary>
public class MySqlLockingExecutionTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task For_update_no_wait_fails_immediately_on_a_row_another_transaction_holds()
    {
        await fixture.ResetAsync();
        await using (var seed = fixture.CreateContext())
        {
            seed.Blogs.Add(new Blog { Name = "locked", IsActive = true, Rating = 1, CreatedAt = DateTime.UtcNow, ExternalId = Guid.NewGuid() });
            await seed.SaveChangesAsync(Ct);
        }

        await using var holder = fixture.CreateContext();
        await using var transaction = await holder.Database.BeginTransactionAsync(Ct);
        await holder.Blogs.Where(b => b.Name == "locked").ForUpdate().SingleAsync(Ct);

        await using var contender = fixture.CreateContext();

        var exception = await Assert.ThrowsAsync<MySqlConnector.MySqlException>(
            () => contender.Blogs.Where(b => b.Name == "locked").ForUpdateNoWait().SingleAsync(Ct));
        Assert.Contains("lock", exception.Message, StringComparison.OrdinalIgnoreCase);

        await transaction.RollbackAsync(Ct);
    }

    [Fact]
    public async Task For_update_skip_locked_excludes_a_row_another_transaction_holds()
    {
        await fixture.ResetAsync();
        await using (var seed = fixture.CreateContext())
        {
            seed.Blogs.Add(new Blog { Name = "held", IsActive = true, Rating = 1, CreatedAt = DateTime.UtcNow, ExternalId = Guid.NewGuid() });
            seed.Blogs.Add(new Blog { Name = "free", IsActive = true, Rating = 1, CreatedAt = DateTime.UtcNow, ExternalId = Guid.NewGuid() });
            await seed.SaveChangesAsync(Ct);
        }

        await using var holder = fixture.CreateContext();
        await using var transaction = await holder.Database.BeginTransactionAsync(Ct);
        await holder.Blogs.Where(b => b.Name == "held").ForUpdate().SingleAsync(Ct);

        await using var contender = fixture.CreateContext();
        var names = await contender.Blogs.ForUpdateSkipLocked().Select(b => b.Name).OrderBy(n => n).ToListAsync(Ct);

        Assert.Equal(["free"], names);

        await transaction.RollbackAsync(Ct);
    }
}

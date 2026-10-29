using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>SQL shape: an INSERT whose ON DUPLICATE KEY UPDATE clause excludes the key properties by default.</summary>
public class MySqlUpsertTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_mapped_property_but_the_key_is_inserted_and_updated_on_conflict()
    {
        var db = new FakeMySql();
        await using var context = db.Context<BlogContext>(options => new BlogContext(options));
        var blog = new Blog { Id = 1, Name = "a", IsActive = true, Rating = 1, CreatedAt = new DateTime(2026, 1, 1), ExternalId = Guid.NewGuid() };
        db.Connection.NonQueryResults.Enqueue(1);

        await context.UpsertAsync(blog, ["Id"], cancellationToken: Ct);

        var sql = db.Commands.Single();
        Assert.StartsWith("INSERT INTO `Blogs` (", sql);
        Assert.Contains(" VALUES (", sql);
        foreach (var column in new[] { "Id", "Name", "IsActive", "Rating", "CreatedAt", "ExternalId" })
        {
            Assert.Contains($"`{column}`", sql);
        }

        var updateClause = sql[sql.IndexOf("ON DUPLICATE", StringComparison.Ordinal)..];
        foreach (var column in new[] { "Name", "IsActive", "Rating", "CreatedAt", "ExternalId" })
        {
            Assert.Contains($"`{column}` = VALUES(`{column}`)", updateClause);
        }

        Assert.DoesNotContain("`Id` = VALUES(`Id`)", updateClause);
    }

    [Fact]
    public async Task Update_properties_can_be_named_explicitly()
    {
        var db = new FakeMySql();
        await using var context = db.Context<BlogContext>(options => new BlogContext(options));
        var blog = new Blog { Id = 1, Name = "a", IsActive = true, Rating = 1, CreatedAt = new DateTime(2026, 1, 1), ExternalId = Guid.NewGuid() };
        db.Connection.NonQueryResults.Enqueue(1);

        await context.UpsertAsync(blog, ["Id"], ["Name"], Ct);

        Assert.Equal(
            "ON DUPLICATE KEY UPDATE `Name` = VALUES(`Name`)",
            db.Commands.Single()[db.Commands.Single().IndexOf("ON DUPLICATE", StringComparison.Ordinal)..]);
    }

    [Fact]
    public async Task Every_property_as_a_key_leaves_nothing_to_update()
    {
        var db = new FakeMySql();
        await using var context = db.Context<BlogContext>(options => new BlogContext(options));
        var blog = new Blog { Id = 1, Name = "a", IsActive = true, Rating = 1, CreatedAt = new DateTime(2026, 1, 1), ExternalId = Guid.NewGuid() };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.UpsertAsync(blog, ["Id", "Name", "IsActive", "Rating", "CreatedAt", "ExternalId"], cancellationToken: Ct));
    }
}

/// <summary>The upsert is real: a second call with the same key updates the row MySQL already has for it.</summary>
public class MySqlUpsertExecutionTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_conflicting_key_updates_the_row_instead_of_inserting_a_second_one()
    {
        await fixture.ResetAsync();
        var id = Random.Shared.Next(1, int.MaxValue);
        var externalId = Guid.NewGuid();

        await using var first = fixture.CreateContext();
        var inserted = await first.UpsertAsync(
            new Blog { Id = id, Name = "first", IsActive = true, Rating = 1, CreatedAt = new DateTime(2026, 1, 1), ExternalId = externalId },
            ["Id"], cancellationToken: Ct);

        await using var second = fixture.CreateContext();
        var updated = await second.UpsertAsync(
            new Blog { Id = id, Name = "second", IsActive = false, Rating = 2, CreatedAt = new DateTime(2026, 1, 2), ExternalId = externalId },
            ["Id"], cancellationToken: Ct);

        await using var verify = fixture.CreateContext();
        var row = await verify.Blogs.SingleAsync(b => b.Id == id, Ct);

        Assert.Equal(1, inserted);
        Assert.Equal(2, updated);
        Assert.Equal("second", row.Name);
        Assert.False(row.IsActive);
        Assert.Equal(2, row.Rating);
    }

    [Fact]
    public async Task An_update_that_changes_nothing_still_reports_a_row_because_affected_rows_is_disabled()
    {
        await fixture.ResetAsync();
        var id = Random.Shared.Next(1, int.MaxValue);
        var row = new Blog { Id = id, Name = "same", IsActive = true, Rating = 1, CreatedAt = new DateTime(2026, 1, 1), ExternalId = Guid.NewGuid() };

        await using var first = fixture.CreateContext();
        await first.UpsertAsync(row, ["Id"], cancellationToken: Ct);

        await using var second = fixture.CreateContext();
        var result = await second.UpsertAsync(row, ["Id"], cancellationToken: Ct);

        // MySQL reports 0 for a no-op ON DUPLICATE KEY UPDATE unless CLIENT_FOUND_ROWS is set; this provider's
        // connections set Use Affected Rows=false (MySqlDialect.ConnectionOptions), which is that flag.
        Assert.Equal(1, result);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     One suite, written only against EF Core and ADO.NET abstractions, run unchanged against every provider.
/// </summary>
public abstract class ProviderConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();

        await Fixture.ResetAsync();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public void Context_uses_the_expected_dialect()
    {
        using var context = Fixture.CreateContext();

        Assert.Same(Fixture.ExpectedDialect, context.GetService<IAdoNetDialect>());
    }

    // A generated key is never handed out twice: deleting the row with the highest key doesn't give its key to the next
    // insert (SQLite's rowid would without AUTOINCREMENT), which EF Core's fix-up would confuse with the deleted row.
    [Fact]
    public async Task A_deleted_rows_generated_key_is_not_reused()
    {
        int deletedId;
        await using (var context = Fixture.CreateContext())
        {
            var first = NewBlog("first");
            context.Add(first);
            await context.SaveChangesAsync(Ct);
            deletedId = first.Id;
            context.Remove(first);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            var second = NewBlog("second");
            context.Add(second);
            await context.SaveChangesAsync(Ct);
            Assert.NotEqual(deletedId, second.Id);
        }
    }

    // A database without stored procedures refuses a model mapped to them when the context starts, naming the entity type and
    // the database, as Microsoft's SQLite provider does; one with them starts. Nothing is sent to the database.
    [Fact]
    public void A_model_mapped_to_stored_procedures_starts_only_where_the_database_has_them()
    {
        using var context = Fixture.CreateContext<ProcedureMappedContext>(o => new ProcedureMappedContext(o));

        if (Fixture.ExpectedDialect.Sql.SupportsStoredProcedures)
        {
            Assert.NotNull(context.Model.FindEntityType(typeof(ProcedureWidget)));
            return;
        }

        var error = Assert.ThrowsAny<InvalidOperationException>(() => context.Model);
        Assert.Contains(nameof(ProcedureWidget), error.Message);
        Assert.Contains(Fixture.ExpectedDialect.Name, error.Message);
    }

    [Fact]
    public async Task Insert_reads_back_generated_keys()
    {
        await using var context = Fixture.CreateContext();
        var blog = NewBlog("one");
        blog.Posts.Add(new Post { Title = "first" });
        blog.Posts.Add(new Post { Title = "second" });

        context.Add(blog);
        await context.SaveChangesAsync(Ct);

        Assert.True(blog.Id > 0);
        Assert.All(blog.Posts, p => Assert.Equal(blog.Id, p.BlogId));
        Assert.Equal(2, blog.Posts.Select(p => p.Id).Distinct().Count());
        Assert.DoesNotContain(0, blog.Posts.Select(p => p.Id));
    }

    [Fact]
    public async Task Insert_of_a_row_with_no_columns_to_write_reads_back_its_key()
    {
        await using var context = Fixture.CreateContext();
        var tallies = new[] { new Tally(), new Tally() };

        context.AddRange(tallies);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(2, tallies.Select(t => t.Id).Distinct().Count());
        Assert.DoesNotContain(0, tallies.Select(t => t.Id));
    }

    /// <summary>
    ///     EF sends a key the application set, even on a column the database generates, and the row keeps it: two tables in
    ///     one SaveChanges, mixed with rows whose keys the database numbers. Behaviour reference (not copied):
    ///     https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Specification.Tests/StoreGeneratedTestBase.cs
    ///     Runs on: MySQL, PostgreSQL, SQL Server, flatfile, SQLite, and DuckDB.
    /// </summary>
    [Fact]
    public async Task Insert_keeps_explicit_values_for_generated_keys()
    {
        var explicitBlog = NewBlog("explicit");
        explicitBlog.Id = 1_000_001;
        explicitBlog.Posts.Add(new Post { Id = 2_000_001, Title = "explicit post" });
        explicitBlog.Posts.Add(new Post { Title = "generated post" });
        var generatedBlog = NewBlog("generated");
        var laterBlog = NewBlog("later");
        laterBlog.Id = 1_000_002;

        await using (var context = Fixture.CreateContext())
        {
            context.AddRange(explicitBlog, generatedBlog, laterBlog);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            var blogs = await context.Blogs.OrderBy(b => b.Name).Select(b => new { b.Name, b.Id }).ToListAsync(Ct);
            var posts = await context.Posts.OrderBy(p => p.Title).Select(p => new { p.Title, p.Id, p.BlogId }).ToListAsync(Ct);

            Assert.Equal(1_000_001, blogs.Single(b => b.Name == "explicit").Id);
            Assert.Equal(1_000_002, blogs.Single(b => b.Name == "later").Id);
            Assert.Equal(generatedBlog.Id, blogs.Single(b => b.Name == "generated").Id);
            Assert.DoesNotContain(generatedBlog.Id, new[] { 0, 1_000_001, 1_000_002 });
            Assert.Equal(2_000_001, posts.Single(p => p.Title == "explicit post").Id);
            Assert.All(posts, p => Assert.Equal(1_000_001, p.BlogId));
        }
    }

    /// <summary>
    ///     A parameter whose type only the value says (a raw query's argument) reads back the same whatever the current culture:
    ///     DuckDB.NET 1.5.6.1 sends one as the culture's text (https://github.com/Giorgi/DuckDB.NET/pull/382), so 1234.5 is
    ///     1234,5 under de-DE. Runs on: MySQL, PostgreSQL, SQL Server, flatfile, SQLite, and DuckDB.
    /// </summary>
    [Fact]
    public async Task Raw_query_arguments_read_back_under_a_comma_decimal_culture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        try
        {
            await using var context = Fixture.CreateContext();
            // The value is a parameter; only the quoted column name is SQL text.
            var sql = "SELECT {0} AS " + Fixture.Quote("Value");

            Assert.Equal(1234.5m, await context.Database.SqlQueryRaw<decimal>(sql, 1234.5m).SingleAsync(Ct));
            Assert.Equal(-0.25, await context.Database.SqlQueryRaw<double>(sql, -0.25).SingleAsync(Ct));
            Assert.Equal(
                new DateTime(2026, 12, 31, 23, 59, 58),
                await context.Database.SqlQueryRaw<DateTime>(sql, new DateTime(2026, 12, 31, 23, 59, 58)).SingleAsync(Ct));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public async Task Round_trips_every_mapped_type()
    {
        var expected = NewBlog("types", rating: 12.34m, active: true);
        await using (var context = Fixture.CreateContext())
        {
            context.Add(expected);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            var actual = await context.Blogs.SingleAsync(b => b.Id == expected.Id, Ct);

            Assert.Equal(expected.Name, actual.Name);
            Assert.True(actual.IsActive);
            Assert.Equal(12.34m, actual.Rating);
            Assert.Equal(expected.CreatedAt, actual.CreatedAt);
            Assert.Equal(expected.ExternalId, actual.ExternalId);
        }
    }

    [Fact]
    public async Task Query_filters_orders_and_pages()
    {
        await SeedAsync(10);
        await using var context = Fixture.CreateContext();
        var minRating = 3m;

        var page = await context.Blogs
            .Where(b => b.Rating >= minRating && b.IsActive)
            .OrderByDescending(b => b.Rating)
            .ThenBy(b => b.Name)
            .Skip(1)
            .Take(2)
            .Select(b => b.Name)
            .ToListAsync(Ct);

        // Active blogs are the even ones: ratings 8, 6, 4 (>= 3); skip 1, take 2.
        Assert.Equal(["blog6", "blog4"], page);
    }

    [Fact]
    public async Task Same_query_with_different_paging_values()
    {
        await SeedAsync(5);
        await using var context = Fixture.CreateContext();

        foreach (var (skip, take) in new[] { (0, 1), (1, 3), (4, 10) })
        {
            var names = await context.Blogs.OrderBy(b => b.Rating).Skip(skip).Take(take).Select(b => b.Name).ToListAsync(Ct);

            Assert.Equal(Enumerable.Range(skip, Math.Min(take, 5 - skip)).Select(i => $"blog{i}"), names);
        }
    }

    [Fact]
    public async Task Query_offset_without_limit()
    {
        await SeedAsync(5);
        await using var context = Fixture.CreateContext();

        var names = await context.Blogs.OrderBy(b => b.Rating).Skip(3).Select(b => b.Name).ToListAsync(Ct);

        Assert.Equal(["blog3", "blog4"], names);
    }

    [Fact]
    public async Task Query_aggregates_and_boolean_constants()
    {
        await SeedAsync(5);
        await using var context = Fixture.CreateContext();

        Assert.Equal(5, await context.Blogs.CountAsync(Ct));
        Assert.Equal(5L, await context.Blogs.LongCountAsync(Ct));
        Assert.Equal(3, await context.Blogs.CountAsync(b => b.IsActive == true, Ct));
        Assert.Equal(2, await context.Blogs.CountAsync(b => !b.IsActive, Ct));
        Assert.True(await context.Blogs.AnyAsync(b => b.Name == "blog4", Ct));
        Assert.Equal(4m, await context.Blogs.MaxAsync(b => b.Rating, Ct));
    }

    [Fact]
    public async Task Query_includes_collection_navigation()
    {
        await using (var context = Fixture.CreateContext())
        {
            var blog = NewBlog("with-posts");
            blog.Posts.AddRange([new Post { Title = "a" }, new Post { Title = "b" }]);
            context.Add(blog);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            var blog = await context.Blogs.Include(b => b.Posts).SingleAsync(Ct);

            Assert.Equal(["a", "b"], blog.Posts.Select(p => p.Title).Order());
        }
    }

    [Fact]
    public async Task Update_and_delete()
    {
        await SeedAsync(3);

        await using (var context = Fixture.CreateContext())
        {
            var blogs = await context.Blogs.OrderBy(b => b.Rating).ToListAsync(Ct);
            blogs[0].Rating = 99m;
            context.Remove(blogs[1]);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            var remaining = await context.Blogs.OrderBy(b => b.Name).Select(b => new { b.Name, b.Rating }).ToListAsync(Ct);

            Assert.Equal([new { Name = "blog0", Rating = 99m }, new { Name = "blog2", Rating = 2m }], remaining);
        }
    }

    [Fact]
    public async Task Concurrency_conflict_is_detected()
    {
        await SeedAsync(1);
        await using var context = Fixture.CreateContext();
        var blog = await context.Blogs.SingleAsync(Ct);

        await Fixture.ExecuteAsync(
            $"UPDATE {Fixture.Quote("Blogs")} SET {Fixture.Quote("Name")} = 'changed elsewhere' WHERE {Fixture.Quote("Id")} = {blog.Id}");

        blog.Name = "mine";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Rolled_back_transaction_leaves_no_rows()
    {
        await using (var context = Fixture.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);
            context.Add(NewBlog("rolled-back"));
            await context.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(0, await context.Blogs.CountAsync(Ct));
        }
    }

    [Fact]
    public async Task Connection_is_closed_between_operations()
    {
        await SeedAsync(1);
        await using var context = Fixture.CreateContext();

        await context.Blogs.ToListAsync(Ct);

        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Has_tables()
    {
        await using var context = Fixture.CreateContext();

        Assert.True(await context.Database.CanConnectAsync(Ct));
        Assert.True(context.GetService<IRelationalDatabaseCreator>().HasTables());
    }

    [Fact]
    public async Task Savepoints_undo_part_of_a_transaction()
    {
        await using (var context = Fixture.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);

            // The dialect says whether the database has savepoints; the driver's flag only says whether it implements them.
            Assert.Equal(Fixture.ExpectedDialect.Sql.Savepoints != null, transaction.SupportsSavepoints);
            if (!transaction.SupportsSavepoints)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => transaction.CreateSavepointAsync("before_discard", Ct));
                await Assert.ThrowsAsync<NotSupportedException>(() => transaction.RollbackToSavepointAsync("before_discard", Ct));
                await Assert.ThrowsAsync<NotSupportedException>(() => transaction.ReleaseSavepointAsync("before_discard", Ct));
                Fixture.SkipFor("no savepoints");
            }

            context.Add(NewBlog("kept"));
            await context.SaveChangesAsync(Ct);
            await transaction.CreateSavepointAsync("before_discard", Ct);
            context.Add(NewBlog("discarded"));
            await context.SaveChangesAsync(Ct);
            await transaction.RollbackToSavepointAsync("before_discard", Ct);
            await transaction.CommitAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(["kept"], await context.Blogs.Select(b => b.Name).ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task Insert_uses_the_dialects_generated_value_strategy()
    {
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<BlogContext>(Fixture.Options)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted])
            .Options;

        await using var context = new BlogContext(options);
        context.Add(NewBlog("logged"));
        await context.SaveChangesAsync(Ct);

        var insert = Assert.Single(commands, c => c.Contains("INSERT INTO"));
        switch (Fixture.ExpectedDialect.Sql.Keys)
        {
            case ReturningKeyRetrieval:
                Assert.Contains("RETURNING", insert);
                break;
            case DeltaTableKeyRetrieval:
                Assert.DoesNotContain("RETURNING", insert);
                Assert.Contains("FINAL TABLE (INSERT INTO", insert);
                break;
            case SelectAfterKeyRetrieval selectAfter:
                Assert.DoesNotContain("RETURNING", insert);
                Assert.Contains(selectAfter.IdentityFunction, insert);
                break;
            case SequenceKeyRetrieval:
                Assert.DoesNotContain("RETURNING", insert);
                Assert.Contains(commands, c => c.Contains("NEXT VALUE FOR"));
                break;
        }
    }

    [Fact]
    public async Task Execute_update_changes_matching_rows()
    {
        await SeedAsync(4);
        await using (var context = Fixture.CreateContext())
        {
            var updated = await context.Blogs
                .Where(b => b.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Rating, 7m).SetProperty(b => b.IsActive, b => !b.IsActive), Ct);

            Assert.Equal(2, updated);
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(
                ["blog0", "blog2"],
                await context.Blogs.Where(b => b.Rating == 7m && !b.IsActive).OrderBy(b => b.Name).Select(b => b.Name).ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task Execute_delete_removes_matching_rows()
    {
        await SeedAsync(4);
        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(2, await context.Blogs.Where(b => b.Rating >= 2m).ExecuteDeleteAsync(Ct));
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(["blog0", "blog1"], await context.Blogs.OrderBy(b => b.Name).Select(b => b.Name).ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task Execute_delete_filters_through_a_navigation()
    {
        await using (var context = Fixture.CreateContext())
        {
            var active = NewBlog("active", active: true);
            active.Posts.Add(new Post { Title = "kept" });
            var inactive = NewBlog("inactive", active: false);
            inactive.Posts.AddRange([new Post { Title = "gone1" }, new Post { Title = "gone2" }]);
            context.AddRange(active, inactive);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(2, await context.Posts.Where(p => !p.Blog.IsActive).ExecuteDeleteAsync(Ct));
            Assert.Equal(["kept"], await context.Posts.Select(p => p.Title).ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task Execute_update_filters_through_a_navigation()
    {
        await using (var context = Fixture.CreateContext())
        {
            var active = NewBlog("active", active: true);
            active.Posts.AddRange([new Post { Title = "a1" }, new Post { Title = "a2" }]);
            var inactive = NewBlog("inactive", active: false);
            inactive.Posts.Add(new Post { Title = "i1" });
            context.AddRange(active, inactive);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(2, await context.Posts.Where(p => p.Blog.IsActive).ExecuteUpdateAsync(s => s.SetProperty(p => p.Title, "live"), Ct));
            Assert.Equal(["i1", "live", "live"], await context.Posts.OrderBy(p => p.Title).Select(p => p.Title).ToListAsync(Ct));
        }
    }

    [Fact]
    public void Synchronous_insert_query_update_delete_and_transaction()
    {
        var blog = NewBlog("sync");
        blog.Posts.Add(new Post { Title = "p" });
        using (var context = Fixture.CreateContext())
        {
            context.Add(blog);
            context.SaveChanges();
        }

        using (var context = Fixture.CreateContext())
        {
            var loaded = context.Blogs.Single(b => b.Id == blog.Id);
            var post = Assert.Single(context.Posts.Where(p => p.BlogId == blog.Id).ToList());
            Assert.Equal("p", post.Title);

            loaded.Rating = 5m;
            context.Remove(post);
            context.SaveChanges();
        }

        using (var context = Fixture.CreateContext())
        {
            using var transaction = context.Database.BeginTransaction();
            context.Add(NewBlog("rolled-back"));
            context.SaveChanges();
            transaction.Rollback();
        }

        using (var context = Fixture.CreateContext())
        {
            Assert.Equal([("sync", 5m)], context.Blogs.Select(b => new { b.Name, b.Rating }).AsEnumerable().Select(b => (b.Name, b.Rating)));
            Assert.Equal(0, context.Posts.Count());
            Assert.Equal(1, context.Blogs.Where(b => b.Rating == 5m).ExecuteUpdate(s => s.SetProperty(b => b.Name, "renamed")));
            Assert.Equal(1, context.Blogs.ExecuteDelete());
        }
    }

    /// <remarks>
    ///     Where the dialect runs several statements in one command, SaveChanges sends a batch of inserts as one command.
    ///     Runs on: every provider (one command per insert where the dialect can't batch).
    /// </remarks>
    [Fact]
    public async Task Save_changes_batches_inserts_where_the_dialect_can()
    {
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<BlogContext>(Fixture.Options)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted])
            .Options;
        var capabilities = Fixture.ExpectedDialect.Capabilities;
        var batches = capabilities.SupportsMultipleStatementsPerCommand && (capabilities.DefaultMaxBatchSize > 1);

        await using (var context = new BlogContext(options))
        {
            context.AddRange(NewBlog("batched-a"), NewBlog("batched-b"));
            await context.SaveChangesAsync(Ct);
        }

        Assert.Equal(batches ? 1 : 2, commands.Count(c => c.Contains("INSERT", StringComparison.OrdinalIgnoreCase)));
        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(2, await context.Blogs.CountAsync(b => b.Name.StartsWith("batched-"), Ct));
        }
    }

    /// <remarks>
    ///     A decimal wider than <see cref="long" /> at precision 38 (the widest every exact decimal type takes), and an ANSI
    ///     string, round-trip and compare equal to parameters bound with the column's facets. Runs on: every provider.
    /// </remarks>
    [Fact]
    public async Task Wide_decimals_and_ansi_strings_round_trip()
    {
        await Fixture.CreateTablesOnceAsync<LedgerContext>(o => new LedgerContext(o));
        await Fixture.ExecuteAsync($"DELETE FROM {Fixture.Quote("Entries")}");
        var big = 123_456_789_012_345_678_901m;

        await using (var context = Fixture.CreateContext<LedgerContext>(o => new LedgerContext(o)))
        {
            context.Add(new LedgerEntry { Big = big, Code = "ascii" });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext<LedgerContext>(o => new LedgerContext(o)))
        {
            Assert.Equal(1, await context.Entries.CountAsync(e => (e.Big == big) && (e.Code == "ascii"), Ct));
            var entry = await context.Entries.SingleAsync(Ct);
            Assert.Equal((big, "ascii"), (entry.Big, entry.Code));
        }
    }

    protected async Task SeedAsync(int count)
    {
        await using var context = Fixture.CreateContext();
        for (var i = 0; i < count; i++)
        {
            context.Add(NewBlog($"blog{i}", rating: i, active: i % 2 == 0));
        }

        await context.SaveChangesAsync(Ct);
    }

    protected static Blog NewBlog(string name, decimal rating = 1m, bool active = true)
        => new()
        {
            Name = name,
            Rating = rating,
            IsActive = active,
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            ExternalId = Guid.NewGuid()
        };
}

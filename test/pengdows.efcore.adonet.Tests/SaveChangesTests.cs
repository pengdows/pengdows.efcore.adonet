using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>What SaveChanges writes and reads back under each key retrieval strategy and batching capability.</summary>
public class SaveChangesTests
{
    private readonly FakeDatabase _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private static Item NewItem(string name = "n")
        => new() { Name = name };

    [Fact]
    public async Task Returning_reads_the_key_back_from_the_insert()
    {
        await using var context = _db.Context(TestDialects.Ansi);
        var item = NewItem();
        _db.Returns(FakeDatabase.Row("Id", 7));

        context.Add(item);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(7, item.Id);
        Assert.Matches(@"INSERT INTO ""Items"" \(.*\)\nVALUES \(.*\)\nRETURNING ""Id"";", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task Returning_detects_a_concurrency_conflict_when_no_row_comes_back()
    {
        await using var context = _db.Context(TestDialects.Ansi);
        var item = new Item { Id = 1, Version = 3 };
        context.Attach(item);
        item.Name = "changed";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
        Assert.Matches(@"UPDATE ""Items"" SET ""Name"" = @p\d+\nWHERE ""Id"" = @p\d+ AND ""Version"" = @p\d+\nRETURNING 1;", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task Returning_delete_succeeds_when_its_row_comes_back()
    {
        await using var context = _db.Context(TestDialects.Ansi);
        context.Remove(new Item { Id = 1, Version = 3 });
        _db.Returns(FakeDatabase.Row("1", 1));

        await context.SaveChangesAsync(Ct);

        Assert.Contains("DELETE FROM \"Items\"\nWHERE \"Id\" = @p0 AND \"Version\" = @p1\nRETURNING 1;", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task Select_after_reads_the_key_with_the_identity_and_rows_affected_functions()
    {
        await using var context = _db.Context(TestDialects.SelectAfter);
        var item = NewItem();
        _db.Returns(FakeDatabase.Row("Id", 9));

        context.Add(item);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(9, item.Id);
        Assert.Contains("SELECT \"Id\"\nFROM \"Items\"\nWHERE row_count() = 1 AND \"Id\" = last_id();", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task Select_after_checks_updates_and_deletes_with_the_rows_affected_function()
    {
        await using (var context = _db.Context(TestDialects.SelectAfter))
        {
            var item = new Item { Id = 1 };
            context.Attach(item);
            item.Name = "changed";
            _db.Returns(FakeDatabase.Row("count", 0));

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
        }

        await using (var context = _db.Context(TestDialects.SelectAfter))
        {
            context.Remove(new Item { Id = 2 });
            _db.Returns(FakeDatabase.Row("count", 1));

            await context.SaveChangesAsync(Ct);
        }

        Assert.All(_db.Commands, c => Assert.EndsWith("SELECT row_count();", c.TrimEnd()));
        Assert.Contains("UPDATE \"Items\" SET", _db.Commands[0]);
        Assert.Contains("DELETE FROM \"Items\"", _db.Commands[1]);
    }

    // SQL:2023 T495: the statement is a data change delta table, and the key comes back as an ordinary SELECT over it.
    [Fact]
    public async Task A_delta_table_insert_reads_the_key_from_the_final_table()
    {
        await using var context = _db.Context(TestDialects.DeltaTable);
        var item = NewItem();
        _db.Returns(FakeDatabase.Row("Id", 7));

        context.Add(item);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(7, item.Id);
        Assert.Matches(@"^SELECT ""Id""\nFROM FINAL TABLE \(INSERT INTO ""Items"" \(.*\)\nVALUES \(.*\)\);$", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task A_delta_table_update_checks_its_row_in_the_new_table()
    {
        await using var context = _db.Context(TestDialects.DeltaTable);
        var item = new Item { Id = 1, Version = 3 };
        context.Attach(item);
        item.Name = "changed";
        _db.Returns(FakeDatabase.Row("1", 1));

        await context.SaveChangesAsync(Ct);

        Assert.Matches(
            @"^SELECT 1\nFROM NEW TABLE \(UPDATE ""Items"" SET ""Name"" = @p\d+\nWHERE ""Id"" = @p\d+ AND ""Version"" = @p\d+\);$",
            Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task A_delta_table_update_detects_a_concurrency_conflict_when_no_row_comes_back()
    {
        await using var context = _db.Context(TestDialects.DeltaTable);
        var item = new Item { Id = 1, Version = 3 };
        context.Attach(item);
        item.Name = "changed";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task A_delta_table_delete_checks_its_row_in_the_old_table()
    {
        await using var context = _db.Context(TestDialects.DeltaTable);
        context.Remove(new Item { Id = 1, Version = 3 });
        _db.Returns(FakeDatabase.Row("1", 1));

        await context.SaveChangesAsync(Ct);

        Assert.Matches(
            @"^SELECT 1\nFROM OLD TABLE \(DELETE FROM ""Items""\nWHERE ""Id"" = @p\d+ AND ""Version"" = @p\d+\);$",
            Assert.Single(_db.Commands));
    }

    // The identity function names the last number handed out, not a row whose key came from a column default: the key
    // columns the caller supplied identify that row instead.
    [Fact]
    public async Task Select_after_reads_a_defaulted_key_by_the_key_columns_the_caller_supplied()
    {
        await using var context = _db.ContextOf<DefaultedKeyContext>(TestDialects.SelectAfter, o => new DefaultedKeyContext(o));
        var row = new DefaultedKey { Part = 4 };
        _db.Returns(FakeDatabase.Row("Id", 12));

        context.Add(row);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(12, row.Id);
        var command = Assert.Single(_db.Commands);
        Assert.Contains("SELECT \"Id\"\nFROM \"DefaultedKeys\"\nWHERE row_count() = 1 AND \"Part\" = @p", command);
        Assert.DoesNotContain("last_id()", command);
    }

    // A database that returns keys from the insert never needs the supplied columns to find the row.
    [Fact]
    public async Task Returning_reads_a_defaulted_key_from_the_insert()
    {
        await using var context = _db.ContextOf<DefaultedKeyContext>(TestDialects.Ansi, o => new DefaultedKeyContext(o));
        var row = new DefaultedKey { Part = 4 };
        _db.Returns(FakeDatabase.Row("Id", 12));

        context.Add(row);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(12, row.Id);
        Assert.Matches(@"INSERT INTO ""DefaultedKeys"" \(.*\)\nVALUES \(.*\)\nRETURNING ""Id"";", Assert.Single(_db.Commands));
    }

    public class DefaultedKey
    {
        public int Id { get; set; }
        public int Part { get; set; }
    }

    private sealed class DefaultedKeyContext(DbContextOptions<DefaultedKeyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DefaultedKey>(b =>
            {
                b.ToTable("DefaultedKeys");
                b.HasKey(e => new { e.Id, e.Part });
                b.Property(e => e.Id).HasDefaultValueSql("next_id()").ValueGeneratedOnAdd();
                b.Property(e => e.Part).ValueGeneratedNever();
            });
    }

    [Fact]
    public void Keys_a_sequence_cannot_supply_are_generated_on_the_client()
    {
        using var context = _db.ContextOf<GuidKeyedContext>(TestDialects.Sequence, o => new GuidKeyedContext(o));
        var keyed = new GuidKeyed();

        context.Add(keyed);

        Assert.NotEqual(Guid.Empty, keyed.Id);
        Assert.Empty(_db.Commands);
    }

    public class GuidKeyed
    {
        public Guid Id { get; set; }
    }

    public class GuidKeyedContext(DbContextOptions<GuidKeyedContext> options) : DbContext(options)
    {
        public DbSet<GuidKeyed> Keyed => Set<GuidKeyed>();
    }

    [Fact]
    public void A_dialect_names_its_key_sequences_and_how_to_draw_from_them()
    {
        var dialect = new PartsDialect(
            "Named sequences",
            new PartsSql(keys: KeyRetrieval.Sequence((table, column) => $"seq_{table}_{column}", sequence => $"SELECT nextval({sequence})")));
        using var context = _db.Context(dialect);
        _db.Connection.EnqueueScalarResult(7);

        context.Add(NewItem());

        Assert.Equal("SELECT nextval(\"seq_Items_Id\")", _db.Commands[0]);
    }

    [Fact]
    public async Task Sequence_draws_the_key_before_a_plain_insert()
    {
        await using var context = _db.Context(TestDialects.Sequence);
        var item = NewItem();
        _db.Connection.EnqueueScalarResult(42);

        context.Add(item);
        Assert.Equal(42, item.Id);

        // fakeDb 2.0.5 reports RecordsAffected = 0, which is exactly a row that wasn't written.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
        Assert.EndsWith("VALUES (NEXT VALUE FOR \"Items_Id_seq\")", _db.Commands[0]);
        Assert.Matches(@"INSERT INTO ""Items"" \(""Id"", .*\)\nVALUES \(@p0, .*\);$", _db.Commands[1]);
        Assert.DoesNotContain("RETURNING", _db.Commands[1]);
    }

    // EF caches value generators across contexts: each key must still come from the connection of the context saving it,
    // inside its transaction, never from the connection of the context that first asked for a generator.
    [Fact]
    public async Task Sequence_keys_come_from_the_saving_contexts_own_connection()
    {
        var other = new FakeDatabase();
        await using (var first = _db.Context(TestDialects.Sequence))
        {
            _db.Connection.EnqueueScalarResult(42);
            first.Add(NewItem());
        }

        await using var second = other.Context(TestDialects.Sequence);
        other.Connection.EnqueueScalarResult(43);
        var item = NewItem();

        second.Add(item);

        Assert.Equal(43, item.Id);
        Assert.Single(_db.Commands);
        Assert.EndsWith("VALUES (NEXT VALUE FOR \"Items_Id_seq\")", Assert.Single(other.Commands));
    }

    [Fact]
    public void Sequence_works_synchronously_too()
    {
        using var context = _db.Context(TestDialects.Sequence);
        var item = NewItem();
        _db.Connection.EnqueueScalarResult(43);

        context.Add(item);
        Assert.Equal(43, item.Id);

        Assert.Throws<DbUpdateConcurrencyException>(() => context.SaveChanges());
        Assert.Equal(2, _db.Commands.Count);
    }

    [Fact]
    public async Task Sequence_updates_and_deletes_are_plain_statements_checked_by_rows_affected()
    {
        await using (var context = _db.Context(TestDialects.Sequence))
        {
            var item = new Item { Id = 1, Version = 2 };
            context.Attach(item);
            item.Name = "changed";

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
        }

        await using (var context = _db.Context(TestDialects.Sequence))
        {
            context.Remove(new Item { Id = 2, Version = 2 });

            Assert.Throws<DbUpdateConcurrencyException>(() => context.SaveChanges());
        }

        Assert.Equal("UPDATE \"Items\" SET \"Name\" = @p0\nWHERE \"Id\" = @p1 AND \"Version\" = @p2;", _db.Commands[0]);
        Assert.Equal("DELETE FROM \"Items\"\nWHERE \"Id\" = @p0 AND \"Version\" = @p1;", _db.Commands[1]);
    }

    [Fact]
    public async Task Sequence_keys_convert_to_the_key_type()
    {
        await using var context = _db.Context(TestDialects.Sequence);
        _db.Connection.EnqueueScalarResult(5);
        _db.Connection.EnqueueScalarResult(6L);

        var longKeyed = (await context.AddAsync(new LongKeyed(), Ct)).Entity;
        var shortKeyed = (await context.AddAsync(new ShortKeyed(), Ct)).Entity;

        Assert.Equal(5L, longKeyed.Id);
        Assert.Equal((short)6, shortKeyed.Id);
        Assert.EndsWith("VALUES (NEXT VALUE FOR \"LongKeyed_Id_seq\")", _db.Commands[0]);
        Assert.EndsWith("VALUES (NEXT VALUE FOR \"ShortKeyed_Id_seq\")", _db.Commands[1]);
    }

    [Fact]
    public async Task Sequence_rejects_other_store_generated_columns()
    {
        await using var context = _db.Context(TestDialects.Sequence);
        _db.Connection.EnqueueScalarResult(1);
        context.Add(new Defaulted());

        var error = await Assert.ThrowsAsync<NotSupportedException>(() => context.SaveChangesAsync(Ct));

        Assert.Contains("'Defaulted.Counter'", error.Message);
    }

    [Fact]
    public async Task Multi_statement_dialects_batch_up_to_their_default_size()
    {
        await using var context = _db.Context(TestDialects.BatchesOfTwo);
        _db.Connection.EnqueueMultiResultReader([[FakeDatabase.Row("Id", 1)], [FakeDatabase.Row("Id", 2)]]);
        _db.Returns(FakeDatabase.Row("Id", 3));

        context.AddRange(NewItem("a"), NewItem("b"), NewItem("c"));
        await context.SaveChangesAsync(Ct);

        Assert.Equal(2, _db.Commands.Count);
        Assert.Equal(2, CountOf("INSERT INTO", _db.Commands[0]));
    }

    [Fact]
    public async Task Max_batch_size_overrides_the_dialect_default()
    {
        await using var context = _db.Context(TestDialects.BatchesOfTwo, o => o.MaxBatchSize(1));
        _db.Returns(FakeDatabase.Row("Id", 1));
        _db.Returns(FakeDatabase.Row("Id", 2));

        context.AddRange(NewItem("a"), NewItem("b"));
        await context.SaveChangesAsync(Ct);

        Assert.Equal(2, _db.Commands.Count);
    }

    [Fact]
    public async Task Single_statement_dialects_never_batch()
    {
        await using var context = _db.Context(TestDialects.Ansi, o => o.MaxBatchSize(50));
        _db.Returns(FakeDatabase.Row("Id", 1));
        _db.Returns(FakeDatabase.Row("Id", 2));

        context.AddRange(NewItem("a"), NewItem("b"));
        await context.SaveChangesAsync(Ct);

        Assert.Equal(2, _db.Commands.Count);
        Assert.All(_db.Commands, c => Assert.Equal(1, CountOf("INSERT INTO", c)));
    }

    private static int CountOf(string text, string within)
        => (within.Length - within.Replace(text, "").Length) / text.Length;

    // A key drawn from a sequence when the entity is added runs outside SaveChanges, so outside its execution strategy:
    // the draw runs under the context's own, retried like any other command where that strategy retries.
    [Fact]
    public async Task A_sequence_draw_that_fails_transiently_is_retried()
    {
        var events = new List<EventId>();
        await using var context = new ItemContext(new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(_db.Connection, contextOwnsConnection: false, o => o.UseDialect(TestDialects.Sequence).ExecutionStrategy(d => new ImmediateRetry(d)))
            .AllowManyDialects()
            .LogTo((id, _) => id == RelationalEventId.CommandError || id == RelationalEventId.CommandExecuted || id == CoreEventId.ExecutionStrategyRetrying, e => events.Add(e.EventId))
            .Options);
        _db.Connection.SetScalarExecuteException(new TransientFailure());
        _db.Connection.EnqueueScalarResult(42);
        var item = NewItem();

        await context.AddAsync(item, Ct);

        // 42 is the draw after the failed one: fakeDb fails the first scalar and hands out 42 to the next.
        Assert.Equal(42, item.Id);
        Assert.Equal([RelationalEventId.CommandError, CoreEventId.ExecutionStrategyRetrying], events.Take(2));
    }

    // Inside the application's transaction a retrying strategy can't run (a failed statement may have aborted the
    // transaction): the draw runs directly, and a failure surfaces.
    [Fact]
    public async Task A_sequence_draw_inside_a_transaction_is_not_retried()
    {
        await using var context = _db.Context(TestDialects.Sequence, o => o.ExecutionStrategy(d => new ImmediateRetry(d)));
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);
        _db.Connection.EnqueueScalarResult(42);
        var item = NewItem();

        await context.AddAsync(item, Ct);
        Assert.Equal(42, item.Id);

        _db.Connection.SetScalarExecuteException(new TransientFailure());
        await Assert.ThrowsAsync<TransientFailure>(() => context.AddAsync(NewItem(), Ct).AsTask());
    }

    private sealed class ImmediateRetry(ExecutionStrategyDependencies dependencies)
        : AdoNetRetryingExecutionStrategy(dependencies, DatabaseCapabilities.Default, maxRetryCount: 3)
    {
        protected override TimeSpan? GetNextDelay(Exception lastException)
            => base.GetNextDelay(lastException) == null ? null : TimeSpan.Zero;
    }

    private sealed class TransientFailure() : System.Data.Common.DbException("transient")
    {
        public override bool IsTransient
            => true;
    }
}

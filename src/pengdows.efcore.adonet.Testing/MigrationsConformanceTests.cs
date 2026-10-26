using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Testing;

/// <summary>Schema from the model and from migrations, written only against EF Core; run against every dialect.</summary>
public abstract class MigrationsConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    private static readonly string[] OwnTables = ["Keyed", "LongNames", "SchemaCategories", "SchemaBooks", "SchemaAuthors", "Shelves", "Writers", "Authors", "__EFMigrationsHistory", "__EFMigrationsLock"];

    // Only Another_schemas_history_table_is_not_this_ones creates it, and only in the tenant schema.
    private const string TenantHistory = "TenantHistory";

    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();

        await DropOwnTablesAsync();
    }

    public async ValueTask DisposeAsync()
        => await DropOwnTablesAsync();

    [Fact]
    public async Task Create_tables_builds_the_schema_from_the_model()
    {
        await using (var context = Fixture.CreateContext<SchemaContext>(o => new SchemaContext(o)))
        {
            await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext<SchemaContext>(o => new SchemaContext(o)))
        {
            var author = new Author { Name = "Le Guin" };
            author.Books.Add(new Book
            {
                Title = "The Dispossessed", Price = 9.99m, InPrint = true, Published = new DateTime(1974, 5, 1), Isbn = Guid.NewGuid()
            });
            context.Add(author);
            await context.SaveChangesAsync(Ct);

            Assert.True(author.Id > 0);
            Assert.True(author.Books[0].Id > 0);
        }

        await using (var context = Fixture.CreateContext<SchemaContext>(o => new SchemaContext(o)))
        {
            var book = await context.Books.Include(b => b.Author).SingleAsync(Ct);
            Assert.Equal(("Le Guin", 9.99m, true), (book.Author.Name, book.Price, book.InPrint));

            // The unique index on Title.
            context.Add(new Book { Title = "The Dispossessed", AuthorId = book.AuthorId, Isbn = Guid.NewGuid() });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        }
    }

    [Fact]
    public async Task Create_tables_builds_a_table_that_references_itself()
    {
        await using (var context = Fixture.CreateContext<SchemaContext>(o => new SchemaContext(o)))
        {
            await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext<SchemaContext>(o => new SchemaContext(o)))
        {
            context.Add(new Category { Name = "Fiction", Children = { new Category { Name = "Science fiction" } } });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext<SchemaContext>(o => new SchemaContext(o)))
        {
            // Schema, not joins: the rows are read without joining the table to itself.
            var categories = await context.Categories.OrderBy(c => c.Name).ToListAsync(Ct);
            Assert.Equal(["Fiction", "Science fiction"], categories.Select(c => c.Name));
            Assert.Equal(categories[0].Id, categories[1].ParentId);

            // The foreign key holds.
            context.Add(new Category { Name = "Orphan", ParentId = -1 });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        }
    }

    [Fact]
    public async Task Names_longer_than_the_database_takes_are_shortened_and_stay_distinct()
    {
        await using (var context = Fixture.CreateContext<LongNamesContext>(o => new LongNamesContext(o)))
        {
            await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(Ct);
            context.Add(new LongNamed
            {
                TheReferenceNumberThatTheWarehouseAssignsWhenAParcelArrivesAtTheDockOne = 1,
                TheReferenceNumberThatTheWarehouseAssignsWhenAParcelArrivesAtTheDockTwo = 2
            });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext<LongNamesContext>(o => new LongNamesContext(o)))
        {
            var row = await context.LongNames.SingleAsync(Ct);
            Assert.Equal(
                (1, 2),
                (row.TheReferenceNumberThatTheWarehouseAssignsWhenAParcelArrivesAtTheDockOne,
                    row.TheReferenceNumberThatTheWarehouseAssignsWhenAParcelArrivesAtTheDockTwo));
        }
    }

    [Fact]
    public async Task Migrate_applies_records_and_reverts_migrations()
    {
        await using (var context = Fixture.CreateContext<MigrationsContext>(o => new MigrationsContext(o)))
        {
            Assert.Equal([MigrationIds.Initial, MigrationIds.Reshape, MigrationIds.AlterCode], await context.Database.GetPendingMigrationsAsync(Ct));

            await context.Database.MigrateAsync(MigrationIds.Reshape, Ct);

            Assert.Equal([MigrationIds.Initial, MigrationIds.Reshape], await context.Database.GetAppliedMigrationsAsync(Ct));
            Assert.Equal([MigrationIds.AlterCode], await context.Database.GetPendingMigrationsAsync(Ct));
        }

        await using (var context = Fixture.CreateContext<MigrationsContext>(o => new MigrationsContext(o)))
        {
            // Applied again: nothing to do.
            await context.Database.MigrateAsync(MigrationIds.Reshape, Ct);

            // The reshaped schema: Writers.FullName, Shelves.Capacity defaulting to 10, a unique index on Shelves.Label.
            var writer = new Author { Name = "Butler" };
            context.Add(writer);
            await context.SaveChangesAsync(Ct);
            Assert.True(writer.Id > 0);

            var authorAndLabel = $"{Fixture.Quote("AuthorId")}, {Fixture.Quote("Label")}";
            await InsertShelfAsync(authorAndLabel, $"{writer.Id}, 'a'");
            await Assert.ThrowsAnyAsync<Exception>(() => InsertShelfAsync(authorAndLabel, $"{writer.Id}, 'a'"));
        }

        await using (var context = Fixture.CreateContext<MigrationsContext>(o => new MigrationsContext(o)))
        {
            await context.GetService<IMigrator>().MigrateAsync(MigrationIds.Initial, cancellationToken: Ct);
            Assert.Equal([MigrationIds.Initial], await context.Database.GetAppliedMigrationsAsync(Ct));

            await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase, cancellationToken: Ct);
            Assert.Empty(await context.Database.GetAppliedMigrationsAsync(Ct));
        }
    }

    [Fact]
    public void Migrations_apply_synchronously_too()
    {
        using var context = Fixture.CreateContext<MigrationsContext>(o => new MigrationsContext(o));

        context.Database.Migrate(MigrationIds.Reshape);

        Assert.Equal([MigrationIds.Initial, MigrationIds.Reshape], context.Database.GetAppliedMigrations());
    }

    [Fact]
    public async Task Alter_column_changes_size_nullability_and_default()
    {
        await using var context = Fixture.CreateContext<MigrationsContext>(o => new MigrationsContext(o));
        await context.Database.MigrateAsync(MigrationIds.Reshape, Ct);
        var writer = new Author { Name = "Jemisin" };
        context.Add(writer);
        await context.SaveChangesAsync(Ct);

        await context.Database.MigrateAsync(MigrationIds.AlterCode, Ct);

        // Required now, with a default, and 40 characters wide. Labels differ: some databases count NULLs as duplicates in a
        // unique index.
        var shelves = Fixture.Quote("Shelves");
        var (authorId, label, code) = (Fixture.Quote("AuthorId"), Fixture.Quote("Label"), Fixture.Quote("Code"));
        await InsertShelfAsync($"{authorId}, {label}", $"{writer.Id}, 'a'");
        await InsertShelfAsync($"{authorId}, {label}, {code}", $"{writer.Id}, 'b', '{new string('x', 40)}'");
        await Assert.ThrowsAnyAsync<Exception>(() => InsertShelfAsync($"{authorId}, {label}, {code}", $"{writer.Id}, 'c', NULL"));

        // Narrowing back to 20 characters needs the long value gone.
        await Fixture.ExecuteAsync($"DELETE FROM {shelves}");
        await context.GetService<IMigrator>().MigrateAsync(MigrationIds.Reshape, cancellationToken: Ct);
        Assert.Equal([MigrationIds.Initial, MigrationIds.Reshape], await context.Database.GetAppliedMigrationsAsync(Ct));
    }

    // EF begins its migration transaction before generating the SQL, and on failure releases the lock before disposing
    // the transaction: a lock released by a statement would be rolled back with it.
    [Fact]
    public async Task A_migration_that_fails_to_generate_leaves_no_lock_behind()
    {
        await using (var context = Fixture.CreateContext<BrokenMigrationsContext>(o => new BrokenMigrationsContext(o)))
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => context.Database.MigrateAsync(Ct));
        }

        // The next migrator gets the lock at once and fails the same way, rather than waiting the lock out.
        var started = Stopwatch.StartNew();
        await using (var context = Fixture.CreateContext<BrokenMigrationsContext>(o => new BrokenMigrationsContext(o)))
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => context.Database.MigrateAsync(Ct));
        }

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(30), $"The second migration waited {started.Elapsed} for the lock.");
    }

    // A hand-written insert into Shelves. Where keys come from a sequence, the key is drawn from it first, as the dialect's
    // own inserts draw it; elsewhere the database generates it.
    private async Task InsertShelfAsync(string columns, string values)
    {
        var shelves = Fixture.Quote("Shelves");
        if (Fixture.ExpectedDialect.Sql.Keys is not SequenceKeyRetrieval keys)
        {
            await Fixture.ExecuteAsync($"INSERT INTO {shelves} ({columns}) VALUES ({values})");
            return;
        }

        var id = await Fixture.ExecuteScalarAsync(keys.GetNextValueSql(Fixture.Quote(keys.GetSequenceName("Shelves", "Id"))));
        await Fixture.ExecuteAsync($"INSERT INTO {shelves} ({Fixture.Quote("Id")}, {columns}) VALUES ({id}, {values})");
    }

    /// <remarks>
    ///     Some databases can't key or index an unbounded column (SQL Server's nvarchar(max), MySQL's TEXT and BLOB), so a key
    ///     or index column gets a bounded type there; it must still hold a long value. Runs on: every provider.
    /// </remarks>
    [Fact]
    public async Task Unsized_key_and_index_columns_are_created_and_hold_long_values()
    {
        var code = new string('k', 200);
        var indexed = new string('\u00e9', 200);
        var hash = Enumerable.Range(0, 200).Select(i => (byte)i).ToArray();
        await using (var context = Fixture.CreateContext<KeyedContext>(o => new KeyedContext(o)))
        {
            await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(Ct);
            context.Add(new Keyed { Code = code, Indexed = indexed, AnsiIndexed = new string('a', 200), Hash = hash });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext<KeyedContext>(o => new KeyedContext(o)))
        {
            var keyed = await context.Keyed.SingleAsync(k => (k.Code == code) && (k.Indexed == indexed) && (k.Hash == hash), Ct);
            Assert.Equal(new string('a', 200), keyed.AnsiIndexed);

            // The unique index on Indexed.
            context.Add(new Keyed { Code = "other", Indexed = indexed, Hash = [] });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        }
    }

    /// <remarks>
    ///     An unqualified history table is the one in the current schema: one of the same name in another schema doesn't make
    ///     it exist. Runs on: every provider with schemas (skipped where the dialect has none).
    /// </remarks>
    [Fact]
    public async Task Another_schemas_history_table_is_not_this_ones()
    {
        if (!Fixture.ExpectedDialect.Sql.SupportsSchemas)
        {
            Fixture.SkipFor("The database has no schemas.");
        }

        await using (var tenant = Fixture.CreateContext<MigrationsContext>(o => new MigrationsContext(o), o => o.MigrationsHistoryTable(TenantHistory, "tenant")))
        {
            var history = tenant.GetService<IHistoryRepository>();
            if (!await history.ExistsAsync(Ct))
            {
                await history.CreateIfNotExistsAsync(Ct);
            }

            Assert.True(await history.ExistsAsync(Ct));
        }

        await using var context = Fixture.CreateContext<MigrationsContext>(o => new MigrationsContext(o), o => o.MigrationsHistoryTable(TenantHistory));

        Assert.False(await context.GetService<IHistoryRepository>().ExistsAsync(Ct));
    }

    private async Task DropOwnTablesAsync()
    {
        foreach (var table in OwnTables)
        {
            try
            {
                await Fixture.ExecuteAsync($"DROP TABLE {Fixture.Quote(table)}");
            }
            catch (Exception e) when (e is DbException or InvalidOperationException)
            {
                // Expected when the table or sequence isn't there: dropping is how this finds out, portably. pengdows.flatfile
                // reports a missing table as an InvalidOperationException rather than a DbException.
            }
        }

        foreach (var sequence in Fixture.ModelSequences)
        {
            try
            {
                await Fixture.ExecuteAsync($"DROP SEQUENCE {Fixture.Quote(sequence)}");
            }
            catch (Exception e) when (e is DbException or InvalidOperationException)
            {
                // Expected when the table or sequence isn't there: dropping is how this finds out, portably. pengdows.flatfile
                // reports a missing table as an InvalidOperationException rather than a DbException.
            }
        }
    }
}

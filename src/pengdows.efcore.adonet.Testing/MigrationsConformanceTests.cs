using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Testing;

/// <summary>Schema from the model and from migrations, written only against EF Core; run against every dialect.</summary>
public abstract class MigrationsConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    private static readonly string[] OwnTables = ["SchemaBooks", "SchemaAuthors", "Shelves", "Writers", "Authors", "__EFMigrationsHistory", "__EFMigrationsLock"];

    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();

        await DropOwnTablesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Fixture.SkipReason is null)
        {
            await DropOwnTablesAsync();
        }
    }

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

            await Fixture.ExecuteAsync(
                $"INSERT INTO {Fixture.Quote("Shelves")} ({Fixture.Quote("AuthorId")}, {Fixture.Quote("Label")}) VALUES ({writer.Id}, 'a')");
            await Assert.ThrowsAnyAsync<Exception>(() => Fixture.ExecuteAsync(
                $"INSERT INTO {Fixture.Quote("Shelves")} ({Fixture.Quote("AuthorId")}, {Fixture.Quote("Label")}) VALUES ({writer.Id}, 'a')"));
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
        await Fixture.ExecuteAsync($"INSERT INTO {shelves} ({authorId}, {label}) VALUES ({writer.Id}, 'a')");
        await Fixture.ExecuteAsync($"INSERT INTO {shelves} ({authorId}, {label}, {code}) VALUES ({writer.Id}, 'b', '{new string('x', 40)}')");
        await Assert.ThrowsAnyAsync<Exception>(() => Fixture.ExecuteAsync(
            $"INSERT INTO {shelves} ({authorId}, {label}, {code}) VALUES ({writer.Id}, 'c', NULL)"));

        // Narrowing back to 20 characters needs the long value gone.
        await Fixture.ExecuteAsync($"DELETE FROM {shelves}");
        await context.GetService<IMigrator>().MigrateAsync(MigrationIds.Reshape, cancellationToken: Ct);
        Assert.Equal([MigrationIds.Initial, MigrationIds.Reshape], await context.Database.GetAppliedMigrationsAsync(Ct));
    }

    private async Task DropOwnTablesAsync()
    {
        foreach (var table in OwnTables)
        {
            try
            {
                await Fixture.ExecuteAsync($"DROP TABLE {Fixture.Quote(table)}");
            }
            catch (Exception)
            {
                // Not there.
            }
        }

        foreach (var sequence in Fixture.ModelSequences)
        {
            try
            {
                await Fixture.ExecuteAsync($"DROP SEQUENCE {Fixture.Quote(sequence)}");
            }
            catch (Exception)
            {
                // Not there.
            }
        }
    }
}

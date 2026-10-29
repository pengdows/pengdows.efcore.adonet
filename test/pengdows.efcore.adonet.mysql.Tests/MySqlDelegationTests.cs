using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Pomelo's character set and collation delegation: a model's or entity type's HasCharSet (with no delegation, to the
///     database, its tables and their text columns) and UseCollation, each limited to some of those levels with
///     <see cref="DelegationModes" /> or <c>explicitlyDelegateToChildren</c>; EF's own model UseCollation stays the database's
///     only. And UseMySqlComputedColumn: a date and time the server sets on insert and update. DDL shape, and EnsureCreated
///     and an insert through fakeDb. Provider-only: MySQL's DDL. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Metadata/Internal/MySqlAnnotationProvider.cs
/// </summary>
public class MySqlDelegationTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class Note
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int Count { get; set; }
        public Guid Token { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // One context type, a model per configuration: the cache key is the configuration's name.
    public sealed class NoteContext(DbContextOptions<NoteContext> options, string name, Action<ModelBuilder> configure) : DbContext(options)
    {
        public string Name { get; } = name;

        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Note>().Property(n => n.Title).HasMaxLength(50);
            configure(modelBuilder);
        }
    }

    private sealed class ByName : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (((NoteContext)context).Name, designTime);
    }

    private static readonly Dictionary<string, Action<ModelBuilder>> Configurations = new()
    {
        ["model to all"] = m => m.HasCharSet("utf8mb4"),
        ["model to database"] = m => m.HasCharSet("latin1", DelegationModes.ApplyToDatabases),
        ["model to database by bool"] = m => m.HasCharSet("latin1", explicitlyDelegateToChildren: false),
        ["entity to columns"] = m =>
        {
            m.HasCharSet("utf8mb4");
            m.Entity<Note>().HasCharSet("ascii", DelegationModes.ApplyToColumns);
        },
        ["entity collation to table"] = m => m.Entity<Note>().UseCollation("latin1_bin", DelegationModes.ApplyToTables),
        ["entity collation to all by bool"] = m => m.Entity<Note>().UseCollation("latin1_bin", explicitlyDelegateToChildren: true),
        ["model collation to all"] = m => m.UseCollation("utf8mb4_bin", DelegationModes.ApplyToAll),
        ["model collation, EF's"] = m => m.UseCollation("utf8mb4_bin"),
        ["computed"] = m => m.Entity<Note>().Property(n => n.UpdatedAt).UseMySqlComputedColumn()
    };

    private static NoteContext Context(string name, FakeMySql? db = null)
        => db == null
            ? new NoteContext(
                new DbContextOptionsBuilder<NoteContext>().UseAdoNetMySql("Server=localhost").ReplaceService<IModelCacheKeyFactory, ByName>().Options,
                name,
                Configurations[name])
            : db.Context<NoteContext>(
                o => new NoteContext(o, name, Configurations[name]),
                options: o => o.ReplaceService<IModelCacheKeyFactory, ByName>());

    private static string Script(string name)
    {
        using var context = Context(name);
        return context.Database.GenerateCreateScript();
    }

    [Fact]
    public void A_models_character_set_goes_to_the_database_its_tables_and_their_text_columns()
    {
        var script = Script("model to all");

        Assert.Contains("ALTER DATABASE CHARACTER SET utf8mb4;", script);
        Assert.Contains(") DEFAULT CHARACTER SET utf8mb4;", script);
        Assert.Contains($"{Q}Title{Q} VARCHAR(50) CHARACTER SET utf8mb4 NOT NULL", script);
        Assert.Contains($"{Q}Count{Q} INT NOT NULL", script);
        Assert.Contains($"{Q}Token{Q} CHAR(36) NOT NULL", script);
    }

    [Theory]
    [InlineData("model to database")]
    [InlineData("model to database by bool")]
    public void A_models_character_set_for_the_database_only(string name)
    {
        var script = Script(name);

        Assert.Contains("ALTER DATABASE CHARACTER SET latin1;", script);
        Assert.DoesNotContain("DEFAULT CHARACTER SET", script);
        Assert.Contains($"{Q}Title{Q} VARCHAR(50) NOT NULL", script);
    }

    // The entity type's character set reaches its columns only; its table takes the model's.
    [Fact]
    public void An_entity_types_character_set_for_its_columns_only()
    {
        var script = Script("entity to columns");

        Assert.Contains(") DEFAULT CHARACTER SET utf8mb4;", script);
        Assert.Contains($"{Q}Title{Q} VARCHAR(50) CHARACTER SET ascii NOT NULL", script);
    }

    [Fact]
    public void An_entity_types_collation_for_its_table_or_for_everything_below()
    {
        var table = Script("entity collation to table");
        var all = Script("entity collation to all by bool");

        Assert.Contains(") COLLATE latin1_bin;", table);
        Assert.Contains($"{Q}Title{Q} VARCHAR(50) NOT NULL", table);
        Assert.Contains(") COLLATE latin1_bin;", all);
        Assert.Contains($"{Q}Title{Q} VARCHAR(50) NOT NULL COLLATE {Q}latin1_bin{Q}", all);
    }

    [Fact]
    public void A_models_collation_delegated_to_all_and_efs_own_for_the_database()
    {
        var all = Script("model collation to all");
        var efs = Script("model collation, EF's");

        Assert.Contains("ALTER DATABASE COLLATE utf8mb4_bin;", all);
        Assert.Contains(") COLLATE utf8mb4_bin;", all);
        Assert.Contains($"{Q}Title{Q} VARCHAR(50) NOT NULL COLLATE {Q}utf8mb4_bin{Q}", all);
        Assert.Contains("ALTER DATABASE COLLATE utf8mb4_bin;", efs);
        Assert.DoesNotContain(") COLLATE", efs);
        Assert.Contains($"{Q}Title{Q} VARCHAR(50) NOT NULL,", efs);
    }

    [Fact]
    public void A_computed_date_and_time_is_set_by_the_server_on_insert_and_update()
    {
        using var context = Context("computed");

        Assert.Equal(ValueGenerated.OnAddOrUpdate, context.Model.FindEntityType(typeof(Note))!.FindProperty(nameof(Note.UpdatedAt))!.ValueGenerated);
        Assert.Contains($"{Q}UpdatedAt{Q} DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)", context.Database.GenerateCreateScript());
    }

    [Fact]
    public async Task Ensure_created_sends_the_delegated_ddl()
    {
        var db = new FakeMySql();
        await using var context = Context("model to all", db);
        db.Connection.EnqueueScalarResult(0L);

        Assert.True(await context.Database.EnsureCreatedAsync(Ct));

        var ddl = string.Join("\n", db.Commands);
        Assert.Contains("ALTER DATABASE CHARACTER SET utf8mb4;", ddl);
        Assert.Contains($"{Q}Title{Q} VARCHAR(50) CHARACTER SET utf8mb4 NOT NULL", ddl);
    }

    // The server's value is read back after the insert, as a row version's is.
    [Fact]
    public async Task An_insert_reads_back_the_computed_date_and_time()
    {
        var db = new FakeMySql();
        await using var context = Context("computed", db);
        var at = new DateTime(2024, 3, 5, 6, 7, 8);
        db.Returns([("Id", 1), ("UpdatedAt", at)]);

        context.Notes.Add(new Note { Title = "a" });
        await context.SaveChangesAsync(Ct);

        Assert.Equal(at, context.Notes.Local.Single().UpdatedAt);
        Assert.DoesNotContain($"{Q}UpdatedAt{Q}", db.Commands.Single().Split("SELECT")[0]);
        Assert.Contains($"{Q}UpdatedAt{Q}", db.Commands.Single());
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Oracle's MySql.EntityFrameworkCore names (learned from its public signatures and dev.mysql.com, never its code), so an
///     application moving from it compiles with only its <c>using</c>s changed: UseMySQL, AddMySQLServer,
///     AddEntityFrameworkMySQL, EnableRetryOnFailure with error numbers, ForMySQLHasCharset/HasCollation/HasDefaultValue(Sql),
///     UseMySQLAutoIncrementColumn, the MySQLCharset and MySQLCollation attributes, Match with MySQLMatchSearchMode, and the
///     metadata Get/Set extensions. Each forwards to this provider's own API. SQL shape and fakeDb. Provider-only.
/// </summary>
public class MySqlOracleAliasTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [MySQLCharset("utf8mb4")]
    public sealed class Book
    {
        public int Id { get; set; }

        [MySQLCharset("latin1")]
        public string Title { get; set; } = "";

        [MySQLCollation("latin1_spanish_ci")]
        public string Author { get; set; } = "";

        [MySQLCharset("ascii")]
        public string Isbn { get; set; } = "";

        public string Summary { get; set; } = "";
        public long Serial { get; set; }
        public int Pages { get; set; }
        public string Shelf { get; set; } = "";
    }

    public sealed class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class BookContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Book> Books => Set<Book>();
        public DbSet<Tag> Tags => Set<Tag>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>(b =>
            {
                // Fluent overrides the attribute, as Oracle documents.
                b.Property(x => x.Isbn).HasMaxLength(20).ForMySQLHasCharset("binary");
                b.Property(x => x.Title).HasMaxLength(200);
                b.Property(x => x.Author).HasMaxLength(100);
                b.Property(x => x.Summary).HasMaxLength(500).ForMySQLHasCollation("utf8mb4_bin");
                b.Property(x => x.Pages).ForMySQLHasDefaultValue(100);
                b.Property(x => x.Shelf).HasMaxLength(10).ForMySQLHasDefaultValueSql("'A1'");
                b.Property(x => x.Serial).UseMySQLAutoIncrementColumn("bigint");
                b.HasKey(x => x.Serial);
                b.HasIndex(x => x.Summary).IsFullText();
            });
            modelBuilder.Entity<Tag>(b =>
            {
                b.ForMySQLHasCharset("latin1");
                b.ForMySQLHasCollation("latin1_bin");
                b.Property(t => t.Name).HasMaxLength(50);
            });
        }
    }

    private static BookContext Context(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder<BookContext>();
        configure(builder);
        return new BookContext(builder.Options);
    }

    [Fact]
    public void Use_mysql_with_a_connection_string_or_none_is_mysql()
    {
        using var withString = Context(o => o.UseMySQL("Server=localhost"));
        using var withNone = Context(o => o.UseMySQL());
        using var generic = new BookContext(new DbContextOptionsBuilder<BookContext>().UseMySQL("Server=localhost").Options);

        Assert.Same(MySqlDialect.Instance, withString.GetService<IAdoNetDialect>());
        Assert.Same(MySqlDialect.Instance, withNone.GetService<IAdoNetDialect>());
        Assert.True(generic.Database.IsMySql());
    }

    [Fact]
    public async Task Use_mysql_with_a_connection_runs_through_it()
    {
        var db = new FakeMySql();
        db.Returns([("Name", "fiction")]);
        await using var context = new BookContext(db.Logged(new DbContextOptionsBuilder<BookContext>().UseMySQL(db.Connection)).Options);

        var names = await context.Tags.Select(t => t.Name).ToListAsync(Ct);

        Assert.Equal(["fiction"], names);
        Assert.Contains("FROM `Tags` AS `t`", db.Commands.Single());
    }

    // Oracle's overloads: none, a count, and a count, delay and further MySQL error numbers to retry.
    [Fact]
    public void Enable_retry_on_failure_takes_oracles_overloads()
    {
        using var none = Context(o => o.UseMySQL("Server=localhost", m => m.EnableRetryOnFailure()));
        using var count = Context(o => o.UseMySQL("Server=localhost", m => m.EnableRetryOnFailure(3)));
        using var numbers = Context(o => o.UseMySQL("Server=localhost", m => m.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), new List<int> { 1205 })));

        Assert.True(none.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.True(count.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.True(numbers.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    // A MySQL error number is the driver's Number; anything else is not one of them.
    [Fact]
    public void Error_numbers_to_retry_match_a_drivers_number_only()
    {
        var retries = MySqlOracleAliases.RetriesOn([1205]);

        Assert.False(retries(new InvalidOperationException()));
        Assert.False(retries(new TimeoutException()));
    }

    [Fact]
    public void Add_mysql_server_registers_the_context_and_add_entity_framework_mysql_the_provider()
    {
        var services = new ServiceCollection();
        services.AddMySQLServer<BookContext>("Server=localhost", m => m.EnableRetryOnFailure(), o => o.EnableSensitiveDataLogging());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var internalServices = new ServiceCollection().AddEntityFrameworkMySQL().BuildServiceProvider();

        Assert.True(scope.ServiceProvider.GetRequiredService<BookContext>().Database.IsMySql());
        Assert.Same(MySqlDialect.Instance, internalServices.GetService<IAdoNetDialect>());
    }

    private static string Script()
    {
        using var context = Context(o => o.UseMySQL("Server=localhost"));
        return context.Database.GenerateCreateScript();
    }

    // Attribute on the class and its properties, overridden by the fluent call; collations; defaults; AUTO_INCREMENT.
    [Fact]
    public void Attributes_and_fluent_aliases_write_their_ddl()
    {
        var script = Script();

        Assert.Contains("`Title` varchar(200) CHARACTER SET latin1 NOT NULL", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`Isbn` varchar(20) CHARACTER SET binary NOT NULL", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`Author` varchar(100) NOT NULL COLLATE `latin1_spanish_ci`", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`Summary` varchar(500) CHARACTER SET utf8mb4 NOT NULL COLLATE `utf8mb4_bin`", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`Pages` INT DEFAULT 100 NOT NULL", script);
        Assert.Contains("`Shelf` VARCHAR(10) CHARACTER SET utf8mb4 DEFAULT ('A1') NOT NULL", script);
        Assert.Contains("`Serial` bigint AUTO_INCREMENT NOT NULL", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(") DEFAULT CHARACTER SET utf8mb4;", script);
        Assert.Contains(") DEFAULT CHARACTER SET latin1 COLLATE latin1_bin;", script);
    }

    [Fact]
    public async Task Ensure_created_sends_the_aliases_ddl()
    {
        var db = new FakeMySql();
        db.Connection.EnqueueScalarResult(0L);
        await using var context = new BookContext(db.Logged(new DbContextOptionsBuilder<BookContext>().UseMySQL(db.Connection)).Options);

        Assert.True(await context.Database.EnsureCreatedAsync(Ct));

        var ddl = string.Join("\n", db.Commands);
        Assert.Contains("`Title` VARCHAR(200) CHARACTER SET latin1 NOT NULL", ddl);
        Assert.Contains("`Serial` bigint AUTO_INCREMENT NOT NULL", ddl);
        Assert.Contains(") DEFAULT CHARACTER SET latin1 COLLATE latin1_bin;", ddl);
    }

    [Fact]
    public async Task Match_with_oracles_search_mode_runs()
    {
        var db = new FakeMySql();
        db.Returns([("Relevance", 0.5)]);
        await using var context = new BookContext(db.Logged(new DbContextOptionsBuilder<BookContext>().UseMySQL(db.Connection)).Options);

        var relevance = await context.Books
            .Select(b => EF.Functions.Match(new[] { b.Summary, b.Title }, "+cast", MySQLMatchSearchMode.Boolean))
            .SingleAsync(Ct);

        Assert.Equal(0.5, relevance);
        Assert.Contains("MATCH (`b`.`Summary`, `b`.`Title`) AGAINST ('+cast' IN BOOLEAN MODE)", db.Commands.Single());
    }

    // The metadata extensions read what the builders set, and set what the builders read.
    [Fact]
    public void Metadata_extensions_get_and_set_the_same_annotations()
    {
        using var context = Context(o => o.UseMySQL("Server=localhost"));
        var model = context.GetService<IDesignTimeModel>().Model;
        var book = model.FindEntityType(typeof(Book))!;
        var tag = model.FindEntityType(typeof(Tag))!;
        var summaryIndex = book.GetIndexes().Single();

        Assert.Equal("utf8mb4", book.GetCharSet());
        Assert.Equal("latin1", tag.GetCharSet());
        Assert.Equal("latin1_bin", tag.GetCollation());
        Assert.Equal("latin1", ((IReadOnlyTypeBase)tag).GetCharSet());
        Assert.Equal("latin1", book.FindProperty(nameof(Book.Title))!.GetCharSet());
        Assert.True(summaryIndex.IsFullText());
        Assert.Null(summaryIndex.IsSpatial());
        Assert.Equal(MySQLValueGenerationStrategy.IdentityColumn, book.FindProperty(nameof(Book.Serial))!.GetValueGenerationStrategy());
        Assert.Null(book.FindProperty(nameof(Book.Pages))!.GetValueGenerationStrategy());
        Assert.True(book.FindProperty(nameof(Book.Serial))!.IsCompatibleAutoIncrementColumn());
        Assert.False(book.FindProperty(nameof(Book.Title))!.IsCompatibleAutoIncrementColumn());

        var builder = new ModelBuilder();
        var mutable = builder.Entity<Tag>().Metadata;
        var index = builder.Entity<Tag>().HasIndex(t => t.Name).Metadata;
        var key = builder.Entity<Tag>().HasKey(t => t.Id).Metadata;
        mutable.SetCharSet("ascii");
        mutable.SetCollation("ascii_bin");
        mutable.SetTableOptions(new Dictionary<string, string> { ["ENGINE"] = "MyISAM" });
        index.SetIsFullText(true);
        index.SetFullTextParser("ngram");
        index.SetPrefixLength([5]);
        key.SetPrefixLength([3]);
        mutable.FindProperty(nameof(Tag.Name))!.SetCharSet("latin1");
        mutable.FindProperty(nameof(Tag.Id))!.SetValueGenerationStrategy(MySQLValueGenerationStrategy.IdentityColumn);
        var annotated = (IConventionEntityType)builder.Entity<Book>().Metadata;
        annotated.SetCharSet("utf8mb4", fromDataAnnotation: true);

        Assert.Equal("ascii", mutable.GetCharSet());
        Assert.Equal(ConfigurationSource.Explicit, ((IConventionEntityType)mutable).GetCharSetConfigurationSource());
        Assert.Equal("utf8mb4", annotated.GetCharSet());
        Assert.Equal(ConfigurationSource.DataAnnotation, annotated.GetCharSetConfigurationSource());
        Assert.Equal("ascii_bin", mutable.GetCollation());
        Assert.Equal("MyISAM", mutable.GetTableOptions()["ENGINE"]);
        Assert.True(index.IsFullText());
        Assert.Equal("ngram", index.FullTextParser());
        Assert.Equal([5], index.PrefixLength()!);
        Assert.Equal([3], key.PrefixLength()!);
        Assert.Equal("latin1", mutable.FindProperty(nameof(Tag.Name))!.GetCharSet());
        Assert.Equal(MySQLValueGenerationStrategy.IdentityColumn, mutable.FindProperty(nameof(Tag.Id))!.GetValueGenerationStrategy());
    }

    // The convention builder's model-level calls, as a convention plugin would make them.
    [Fact]
    public void Convention_model_builder_sets_the_models_character_set_and_collation()
    {
        var builder = new ModelBuilder();
        var conventionBuilder = ((IConventionModel)builder.Model).Builder;

        Assert.True(conventionBuilder.CanSetCharSet("utf8mb4"));
        conventionBuilder.HasCharSet("utf8mb4");
        conventionBuilder.UseCollation("utf8mb4_bin");

        Assert.Equal("utf8mb4", builder.Model["MySql:CharSet"]);
        Assert.Equal("utf8mb4_bin", builder.Model.GetCollation());
    }
}

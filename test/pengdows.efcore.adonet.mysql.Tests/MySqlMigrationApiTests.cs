using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Pomelo's remaining model and migration APIs: AutoIncrementColumns; DropPrimaryKey and DropUniqueConstraint with
///     recreateForeignKeys (the table's foreign keys dropped first and added again after, since MySQL may hold the key for one of
///     them); MigrationBuilder.IsMySql; the obsolete HasCollation and ForMySql* aliases; and UseMySqlIdentityColumn on a date
///     and time, which is DEFAULT CURRENT_TIMESTAMP(n), not AUTO_INCREMENT. SQL shape, and the migration commands and an insert
///     run through fakeDb. Provider-only. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlMigrationBuilderExtensions.cs
/// </summary>
public class MySqlMigrationApiTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class Blog
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public List<Post> Posts { get; set; } = [];
    }

    public sealed class Post
    {
        public int Id { get; set; }
        public int BlogId { get; set; }
        public string BlogCode { get; set; } = "";
        public Blog Blog { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime StampedAt { get; set; }
        public string Title { get; set; } = "";
    }

    public sealed class BlogContext(DbContextOptions<BlogContext> options) : DbContext(options)
    {
        public DbSet<Blog> Blogs => Set<Blog>();
        public DbSet<Post> Posts => Set<Post>();

#pragma warning disable CS0618 // The obsolete aliases are what is tested.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.AutoIncrementColumns();
            modelBuilder.Entity<Blog>(b =>
            {
                b.Property(x => x.Code).HasMaxLength(20);
                b.HasAlternateKey(x => x.Code);
            });
            modelBuilder.Entity<Post>(b =>
            {
                b.Property(x => x.BlogCode).HasMaxLength(20);
                b.Property(x => x.Title).HasMaxLength(200).HasCollation("utf8mb4_bin");
                b.Property(x => x.CreatedAt).UseMySqlIdentityColumn();
                b.Property(x => x.StampedAt).HasColumnType("timestamp(3)").UseMySqlIdentityColumn();
                b.HasIndex(x => x.Title).ForMySqlIsFullText();
                b.HasIndex(x => x.BlogCode).ForMySqlIsSpatial(false);
                b.HasOne(x => x.Blog).WithMany(x => x.Posts).HasForeignKey(x => x.BlogId);
            });
        }
#pragma warning restore CS0618
    }

    private static BlogContext Context(FakeMySql? db = null, bool mariaDb = false)
        => db == null
            ? new BlogContext(new DbContextOptionsBuilder<BlogContext>().AllowManyDialects()
                .UseAdoNetMySql("Server=localhost", o => o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance))
                .Options)
            : db.Context<BlogContext>(o => new BlogContext(o), mariaDb);

    private static IReadOnlyList<MigrationCommand> Commands(DbContext context, params Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation[] operations)
        => context.GetService<IMigrationsSqlGenerator>().Generate(operations, context.GetService<IDesignTimeModel>().Model);

    [Fact]
    public void Auto_increment_columns_marks_the_model_and_numbers_integer_keys()
    {
        using var context = Context();

        Assert.Equal("IdentityColumn", context.GetService<IDesignTimeModel>().Model["MySql:ValueGenerationStrategy"]);
        Assert.Contains("`Id` INT AUTO_INCREMENT NOT NULL", context.Database.GenerateCreateScript());
    }

    // MySQL may use the key's index for a foreign key whose columns lead it, and then refuses to drop the key.
    [Fact]
    public void Dropping_a_primary_key_can_recreate_the_tables_foreign_keys_around_it()
    {
        using var context = Context();
        var builder = new MigrationBuilder(context.Database.ProviderName);
        builder.DropPrimaryKey("PK_Posts", "Posts", recreateForeignKeys: true);

        var sql = string.Join("", Commands(context, [.. builder.Operations]).Select(c => c.CommandText));

        var dropForeignKey = sql.IndexOf("ALTER TABLE `Posts` DROP FOREIGN KEY `FK_Posts_Blogs_BlogId`;", StringComparison.Ordinal);
        var dropKey = sql.IndexOf("ALTER TABLE `Posts` DROP PRIMARY KEY;", StringComparison.Ordinal);
        var addForeignKey = sql.IndexOf("ALTER TABLE `Posts` ADD CONSTRAINT `FK_Posts_Blogs_BlogId` FOREIGN KEY (`BlogId`) REFERENCES `Blogs` (`Id`)", StringComparison.Ordinal);
        Assert.True((dropForeignKey >= 0) && (dropForeignKey < dropKey) && (dropKey < addForeignKey), sql);
    }

    [Fact]
    public void Dropping_a_primary_key_without_recreating_drops_only_the_key()
    {
        using var context = Context();
        var builder = new MigrationBuilder(context.Database.ProviderName);
        builder.DropPrimaryKey("PK_Posts", "Posts", recreateForeignKeys: false);

        var sql = string.Join("", Commands(context, [.. builder.Operations]).Select(c => c.CommandText));

        Assert.Contains("ALTER TABLE `Posts` DROP PRIMARY KEY;", sql);
        Assert.DoesNotContain("FOREIGN KEY", sql);
    }

    // The unique constraint's foreign keys are those of the table it's on, as Pomelo has them; run through fakeDb as a
    // migration's commands are.
    [Fact]
    public async Task Dropping_a_unique_constraint_can_recreate_the_tables_foreign_keys_and_runs()
    {
        var db = new FakeMySql();
        await using var context = Context(db);
        var builder = new MigrationBuilder(context.Database.ProviderName);
        builder.DropUniqueConstraint("AK_Posts_Title", "Posts", recreateForeignKeys: true);
        var commands = Commands(context, [.. builder.Operations]);

        await context.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(commands, context.GetService<IRelationalConnection>(), Ct);

        Assert.Equal(3, db.Commands.Count);
        Assert.Contains("DROP FOREIGN KEY `FK_Posts_Blogs_BlogId`", db.Commands[0]);
        Assert.Contains("ALTER TABLE `Posts` DROP INDEX `AK_Posts_Title`", db.Commands[1]);
        Assert.Contains("ADD CONSTRAINT `FK_Posts_Blogs_BlogId` FOREIGN KEY", db.Commands[2]);
    }

    [Fact]
    public void Recreating_foreign_keys_needs_the_model()
    {
        using var context = Context();
        var builder = new MigrationBuilder(context.Database.ProviderName);
        builder.DropPrimaryKey("PK_Posts", "Posts", recreateForeignKeys: true);

        Assert.Throws<InvalidOperationException>(() => context.GetService<IMigrationsSqlGenerator>().Generate([.. builder.Operations]));
    }

    // Every MySqlDialect variant is the MySQL plugin's provider, which is what a migration sees.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_migration_builder_is_mysql_for_every_mysql_dialect(bool mariaDb)
    {
        using var context = Context(mariaDb: mariaDb);

        Assert.True(new MigrationBuilder(context.Database.ProviderName).IsMySql());
        Assert.False(new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer").IsMySql());
        Assert.False(new MigrationBuilder(null).IsMySql());
    }

    [Fact]
    public void Obsolete_aliases_still_work()
    {
        using var context = Context();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("`Title` VARCHAR(200) NOT NULL COLLATE `utf8mb4_bin`,", script);
        Assert.Contains("CREATE FULLTEXT INDEX `IX_Posts_Title`", script);
        Assert.DoesNotContain("SPATIAL", script);
    }

    // A date and time the server sets on insert only: no AUTO_INCREMENT, no ON UPDATE.
    [Fact]
    public void An_identity_date_and_time_defaults_to_the_current_timestamp()
    {
        using var context = Context();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("`CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),", script);
        Assert.Contains("`StampedAt` timestamp(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),", script);
        Assert.Equal(
            MySQLValueGenerationStrategy.IdentityColumn,
            context.Model.FindEntityType(typeof(Post))!.FindProperty(nameof(Post.CreatedAt))!.GetValueGenerationStrategy());
    }

    // The insert leaves the column to the server and reads it back, as for a computed column; the key still comes back
    // through LAST_INSERT_ID().
    [Fact]
    public async Task An_insert_reads_back_the_identity_date_and_time()
    {
        var db = new FakeMySql();
        await using var context = Context(db);
        var at = new DateTime(2024, 3, 5, 6, 7, 8);
        db.Returns([("Id", 7), ("CreatedAt", at), ("StampedAt", at)]);

        context.Posts.Add(new Post { BlogId = 1, BlogCode = "b", Title = "t" });
        await context.SaveChangesAsync(Ct);

        var post = context.Posts.Local.Single();
        var insert = db.Commands.Single();
        Assert.Equal((7, at), (post.Id, post.CreatedAt));
        Assert.DoesNotContain("`CreatedAt`", insert.Split("SELECT")[0]);
        Assert.Contains("LAST_INSERT_ID()", insert);
    }
}

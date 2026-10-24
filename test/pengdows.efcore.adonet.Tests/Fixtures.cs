using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using DuckDB.NET.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Dialects;
using pengdows.flatfile;

namespace pengdows.efcore.adonet.Tests;

public class Blog
{
    public int Id { get; set; }

    [ConcurrencyCheck]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; }
    public decimal Rating { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ExternalId { get; set; }
    public List<Post> Posts { get; set; } = [];
}

public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public string Title { get; set; } = "";
    public Blog Blog { get; set; } = null!;
}

public class BlogContext(DbContextOptions<BlogContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Post> Posts => Set<Post>();
}

/// <summary>
///     Everything provider-specific lives here: the factory, the connection string and the DDL.
///     The tests themselves only see <see cref="DbProviderFactory" /> and <see cref="DbContext" />.
/// </summary>
public abstract class ProviderFixture : IAsyncLifetime
{
    private DbContextOptions<BlogContext>? _options;

    public abstract DbProviderFactory Factory { get; }
    public abstract string ConnectionString { get; }
    public abstract AdoNetSqlDialect ExpectedDialect { get; }
    protected abstract IEnumerable<string> Ddl { get; }

    /// <summary>
    ///     Conformance tests that fail because of a bug or gap in the database engine itself (not in this provider),
    ///     keyed by test method name. They are reported as skipped with the reason instead of silently passing.
    /// </summary>
    public virtual IReadOnlyDictionary<string, string> KnownEngineGaps { get; } = new Dictionary<string, string>();

    /// <summary>Overrides detection; null means rely on <see cref="AdoNetSqlDialect.Detect" />.</summary>
    protected virtual AdoNetSqlDialect? DialectOverride => null;

    public DbContextOptions<BlogContext> Options
        => _options ??= new DbContextOptionsBuilder<BlogContext>()
            .UseAdoNet(Factory, ConnectionString, o =>
            {
                if (DialectOverride is { } dialect)
                {
                    o.UseDialect(dialect);
                }
            })
            .Options;

    public BlogContext CreateContext()
        => new(Options);

    public virtual async ValueTask InitializeAsync()
    {
        foreach (var sql in Ddl)
        {
            await ExecuteAsync(sql);
        }
    }

    public virtual ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    public async Task ResetAsync()
    {
        await ExecuteAsync("DELETE FROM \"Posts\"");
        await ExecuteAsync("DELETE FROM \"Blogs\"");
    }

    /// <summary>Plain ADO.NET, bypassing EF, through the same factory.</summary>
    public async Task<int> ExecuteAsync(string sql)
    {
        await using var connection = Factory.CreateConnection()!;
        connection.ConnectionString = ConnectionString;
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync();
    }
}

public class SqliteFixture : ProviderFixture
{
    // A named shared-cache in-memory database lives as long as one connection to it is open.
    private readonly SqliteConnection _keepAlive;

    public SqliteFixture()
    {
        ConnectionString = $"Data Source=adonet-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    public override DbProviderFactory Factory => SqliteFactory.Instance;
    public override string ConnectionString { get; }
    public override AdoNetSqlDialect ExpectedDialect => SqliteSqlDialect.Instance;

    protected override IEnumerable<string> Ddl =>
    [
        """
        CREATE TABLE "Blogs" (
            "Id" INTEGER PRIMARY KEY,
            "Name" TEXT NOT NULL,
            "IsActive" BOOLEAN NOT NULL,
            "Rating" DECIMAL(18,2) NOT NULL,
            "CreatedAt" TIMESTAMP NOT NULL,
            "ExternalId" BLOB NOT NULL)
        """,
        """
        CREATE TABLE "Posts" (
            "Id" INTEGER PRIMARY KEY,
            "BlogId" INTEGER NOT NULL,
            "Title" TEXT NOT NULL)
        """
    ];

    public override async ValueTask DisposeAsync()
    {
        await _keepAlive.DisposeAsync();
        await base.DisposeAsync();
    }
}

public sealed class LegacySqliteFixture : SqliteFixture
{
    public override AdoNetSqlDialect ExpectedDialect => LegacySqliteSqlDialect.Instance;
    protected override AdoNetSqlDialect? DialectOverride => LegacySqliteSqlDialect.Instance;
}

public sealed class DuckDbFixture : ProviderFixture
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"adonet-{Guid.NewGuid():N}.duckdb");

    public override DbProviderFactory Factory => DuckDBClientFactory.Instance;
    public override string ConnectionString => $"Data Source={_path}";
    public override AdoNetSqlDialect ExpectedDialect => DuckDbSqlDialect.Instance;

    protected override IEnumerable<string> Ddl =>
    [
        "CREATE SEQUENCE \"BlogIds\"",
        "CREATE SEQUENCE \"PostIds\"",
        """
        CREATE TABLE "Blogs" (
            "Id" INTEGER PRIMARY KEY DEFAULT nextval('BlogIds'),
            "Name" VARCHAR NOT NULL,
            "IsActive" BOOLEAN NOT NULL,
            "Rating" DECIMAL(18,2) NOT NULL,
            "CreatedAt" TIMESTAMP NOT NULL,
            "ExternalId" UUID NOT NULL)
        """,
        """
        CREATE TABLE "Posts" (
            "Id" INTEGER PRIMARY KEY DEFAULT nextval('PostIds'),
            "BlogId" INTEGER NOT NULL,
            "Title" VARCHAR NOT NULL)
        """
    ];

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_path);
        File.Delete(_path + ".wal");
    }
}

public sealed class FlatFileFixture : ProviderFixture
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"adonet-{Guid.NewGuid():N}");

    public FlatFileFixture()
        => Directory.CreateDirectory(_path);

    public override DbProviderFactory Factory => FlatFileProviderFactory.Instance;
    public override string ConnectionString => $"path={_path}";
    public override AdoNetSqlDialect ExpectedDialect => FlatFileSqlDialect.Instance;

    public override IReadOnlyDictionary<string, string> KnownEngineGaps { get; } = new Dictionary<string, string>
    {
        // EF pages the principal in a derived table: FROM (SELECT ... FETCH FIRST 2 ROWS ONLY) AS "b0". Valid SQL:2008.
        ["Query_includes_collection_navigation"] = "pengdows.flatfile 0.2.1-preview.8 rejects FETCH FIRST inside a derived table"
    };

    // No identity columns: keys come from the "{table}_{column}_seq" sequences the dialect expects.
    protected override IEnumerable<string> Ddl =>
    [
        "CREATE SEQUENCE \"Blogs_Id_seq\"",
        "CREATE SEQUENCE \"Posts_Id_seq\"",
        """
        CREATE TABLE "Blogs" (
            "Id" INTEGER NOT NULL,
            "Name" VARCHAR NOT NULL,
            "IsActive" BOOLEAN NOT NULL,
            "Rating" DECIMAL(18,2) NOT NULL,
            "CreatedAt" TIMESTAMP NOT NULL,
            "ExternalId" UUID NOT NULL,
            CONSTRAINT "PK_Blogs" PRIMARY KEY ("Id"))
        """,
        """
        CREATE TABLE "Posts" (
            "Id" INTEGER NOT NULL,
            "BlogId" INTEGER NOT NULL,
            "Title" VARCHAR NOT NULL,
            CONSTRAINT "PK_Posts" PRIMARY KEY ("Id"))
        """
    ];

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Directory.Delete(_path, recursive: true);
    }
}

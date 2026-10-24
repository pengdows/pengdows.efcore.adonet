using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     Everything provider-specific lives in a fixture: the factory, the connection string, the dialect and the DDL.
///     The tests themselves only see <see cref="DbProviderFactory" /> and <see cref="DbContext" />.
/// </summary>
public abstract class ProviderFixture : IAsyncLifetime
{
    private DbContextOptions<BlogContext>? _options;

    public abstract DbProviderFactory Factory { get; }
    public abstract string ConnectionString { get; }
    public abstract IAdoNetDialect ExpectedDialect { get; }
    protected abstract IEnumerable<string> Ddl { get; }

    /// <summary>
    ///     Conformance tests that fail because of a bug or gap in the database engine itself (not in the dialect),
    ///     keyed by test method name. They are reported as skipped with the reason instead of silently passing.
    /// </summary>
    public virtual IReadOnlyDictionary<string, string> KnownEngineGaps { get; } = new Dictionary<string, string>();

    /// <summary>Sequences the dialect creates for the migrations tests' tables, dropped between tests.</summary>
    public virtual IReadOnlyList<string> ModelSequences { get; } = [];

    /// <summary>Set when the database can't be reached (e.g. no Docker); every test is then reported as skipped.</summary>
    public string? SkipReason { get; protected set; }

    public DbContextOptions<BlogContext> Options
    {
        get
        {
            if (_options == null)
            {
                var builder = new DbContextOptionsBuilder<BlogContext>();
                UseProvider(builder);
                _options = builder.Options;
            }

            return _options;
        }
    }

    /// <summary>How the context is configured; dialect packages override this to use their own entry point.</summary>
    protected virtual void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNet(Factory, ConnectionString, o => o.UseDialect(ExpectedDialect));

    public BlogContext CreateContext()
        => new(Options);

    /// <summary>A context for <typeparamref name="TContext" /> configured like <see cref="Options" />.</summary>
    public TContext CreateContext<TContext>(Func<DbContextOptions<TContext>, TContext> create)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        UseProvider(builder);
        return create(builder.ForTestMigrations().Options);
    }

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
        await ExecuteAsync($"DELETE FROM {Quote("Posts")}");
        await ExecuteAsync($"DELETE FROM {Quote("Blogs")}");
    }

    /// <summary>Delimits an identifier for hand-written SQL.</summary>
    public string Quote(string identifier)
        => ExpectedDialect.Sql.IdentifierQuotePrefix + identifier + ExpectedDialect.Sql.IdentifierQuoteSuffix;

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

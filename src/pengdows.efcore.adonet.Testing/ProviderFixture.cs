using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     Everything provider-specific lives in a fixture: the factory, the connection string, the dialect and the DDL.
///     The tests themselves only see <see cref="DbProviderFactory" /> and <see cref="DbContext" />.
/// </summary>
public abstract class ProviderFixture : IAsyncLifetime
{
    private readonly HashSet<Type> _created = [];
    private DbContextOptions<BlogContext>? _options;

    public abstract DbProviderFactory Factory { get; }
    public abstract string ConnectionString { get; }
    public abstract IAdoNetDialect ExpectedDialect { get; }
    protected abstract IEnumerable<string> Ddl { get; }

    /// <summary>
    ///     Conformance tests the database can't pass because it lacks the feature under test (or has a bug in it), keyed by
    ///     test method name: never a gap in the dialect. They are reported as skipped, attributed
    ///     <c>[SKIP:&lt;dialect name&gt;]</c>, with the reason.
    /// </summary>
    public virtual IReadOnlyDictionary<string, string> KnownEngineGaps { get; } = new Dictionary<string, string>();

    /// <summary>The offset a <see cref="DateTimeOffset" /> is saved with: one that moves the date, unless the driver refuses it.</summary>
    public virtual TimeSpan StorableOffset
        => TimeSpan.FromHours(2);

    /// <summary>The largest float the database stores: <see cref="float.MaxValue" />, unless its single-precision type stops short.</summary>
    public virtual float LargestStorableFloat
        => float.MaxValue;

    /// <summary>Sequences the dialect creates for the migrations tests' tables, dropped between tests.</summary>
    public virtual IReadOnlyList<string> ModelSequences { get; } = [];


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
        => CreateContext(create, _ => { });

    /// <summary>A context for <typeparamref name="TContext" /> configured like <see cref="Options" />, then by <paramref name="configure" />.</summary>
    public TContext CreateContext<TContext>(Func<DbContextOptions<TContext>, TContext> create, Action<AdoNetDbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        UseProvider(builder);
        configure(new AdoNetDbContextOptionsBuilder(builder));
        return create(builder.ForTestMigrations().Options);
    }

    /// <summary>Skips the calling test when it is one of <see cref="KnownEngineGaps" />.</summary>
    public void SkipIfNotRunnable()
    {
        if (KnownEngineGaps.TryGetValue(TestContext.Current.Test!.TestCase.TestMethodName ?? "", out var reason))
        {
            SkipFor(reason);
        }
    }

    /// <summary>Skips the calling test because the database lacks what it tests: <c>[SKIP:&lt;dialect name&gt;] reason</c>.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public void SkipFor(string reason)
        => Assert.Skip($"[SKIP:{ExpectedDialect.Name}] {reason}");

    /// <summary>Creates <typeparamref name="TContext" />'s tables, through the dialect's own DDL, the first time it is asked.</summary>
    public async Task CreateTablesOnceAsync<TContext>(Func<DbContextOptions<TContext>, TContext> create)
        where TContext : DbContext
    {
        if (_created.Contains(typeof(TContext)))
        {
            return;
        }

        await using var context = CreateContext(create);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        _created.Add(typeof(TContext));
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
        await ExecuteAsync($"DELETE FROM {Quote("Tallies")}");
        await ExecuteAsync($"DELETE FROM {Quote("Posts")}");
        await ExecuteAsync($"DELETE FROM {Quote("Blogs")}");
    }

    /// <summary>
    ///     A connection string for another database, <paramref name="database" />, beside this fixture's: on the same server,
    ///     or in the same directory (the temporary directory, for one in memory). It need not exist.
    /// </summary>
    public virtual string ConnectionStringFor(string database)
    {
        var lifecycle = ExpectedDialect.Sql.Databases
            ?? throw new NotSupportedException($"{ExpectedDialect.Name} declares no DatabaseLifecycle to name another database by.");
        var builder = Factory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        builder.ConnectionString = ConnectionString;
        if (lifecycle.Storage == DatabaseStorage.Server)
        {
            builder[lifecycle.Keyword] = database;
            return builder.ConnectionString;
        }

        var directory = lifecycle.IsInMemory(builder) || !builder.TryGetValue(lifecycle.Keyword, out var path)
            ? Path.GetTempPath()
            : Path.GetDirectoryName(Path.GetFullPath(Convert.ToString(path, System.Globalization.CultureInfo.InvariantCulture)!))!;
        var fresh = Factory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        fresh[lifecycle.Keyword] = Path.Combine(directory, lifecycle.Storage == DatabaseStorage.File ? database + ".db" : database);
        return fresh.ConnectionString;
    }

    /// <summary>Delimits an identifier for hand-written SQL.</summary>
    public string Quote(string identifier)
        => ExpectedDialect.Sql.IdentifierQuotePrefix + identifier + ExpectedDialect.Sql.IdentifierQuoteSuffix;

    /// <summary>The first value <paramref name="sql" /> returns, through plain ADO.NET like <see cref="ExecuteAsync" />.</summary>
    public async Task<object?> ExecuteScalarAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    /// <summary>Plain ADO.NET, bypassing EF, through the same factory.</summary>
    public async Task<int> ExecuteAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync();
    }

    // A session in the mode the dialect writes SQL for, as the provider's own sessions are (ISqlDialect.SessionSetup).
    private async Task<DbConnection> OpenAsync()
    {
        var connection = Factory.CreateConnection()!;
        connection.ConnectionString = ConnectionString;
        await connection.OpenAsync();
        if (ExpectedDialect.Sql.SessionSetup is { } setup)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = setup;
            await command.ExecuteNonQueryAsync();
        }

        return connection;
    }
}

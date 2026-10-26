using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     The extension boundary: everything here is written the way a dialect package outside this repository would write it.
/// </summary>
public class DialectContractTests
{
    private static BlogContext Context(IAdoNetDialect dialect, Action<DbContextOptionsBuilder>? configure = null)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>()
            .UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", o => o.UseDialect(dialect))
            .AllowManyDialects();
        configure?.Invoke(builder);
        return new BlogContext(builder.Options);
    }

    [Fact]
    public void A_dialect_composes_words_and_grammar_parts()
    {
        using var context = Context(ComposedDialect.Instance);

        var sql = context.Blogs.Where(b => b.IsActive).Take(1).ToQueryString();

        Assert.Matches(@"SELECT TOP\(:p\w*\) ", sql);
        Assert.Contains("WHERE [b].[IsActive] = 1", sql);
    }

    [Fact]
    public void Paging_can_require_literal_values()
    {
        using var context = Context(LiteralPagingDialect.Instance);

        var skip = 5;
        var sql = context.Blogs.OrderBy(b => b.Id).Skip(skip).Take(10).ToQueryString();

        Assert.Contains("OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY", sql);
    }

    [Fact]
    public void Services_a_dialect_registers_win_over_the_core_defaults()
    {
        using var context = Context(ServiceDialect.Instance);

        Assert.IsType<ServiceDialect.HistoryRepository>(context.GetService<IHistoryRepository>());
    }

    [Fact]
    public void Each_part_of_the_dialect_is_available_to_services()
    {
        using var context = Context(ComposedDialect.Instance);

        Assert.Same(ComposedDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Same(ComposedDialect.Instance.Sql, context.GetService<ISqlDialect>());
        Assert.Same(ComposedDialect.Instance.TypeMappings, context.GetService<ITypeMappingDialect>());
        Assert.Same(ComposedDialect.Instance.Capabilities, context.GetService<IDatabaseCapabilities>());
    }

    [Fact]
    public void Core_defines_no_dialect_but_ansi()
    {
        var dialects = typeof(IAdoNetDialect).Assembly.GetTypes()
            .Where(t => typeof(IAdoNetDialect).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        Assert.Equal([typeof(AnsiDialect)], dialects);
    }

    [Fact]
    public void Switching_databases_changes_only_the_factory_and_the_dialect()
    {
        using var generic = new BlogContext(
            new DbContextOptionsBuilder<BlogContext>().UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", ComposedDialect.Instance)
                .AllowManyDialects().Options);
        var builder = new DbContextOptionsBuilder<BlogContext>().AllowManyDialects();
        ((DbContextOptionsBuilder)builder).UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", ComposedDialect.Instance, o => o.MaxBatchSize(7));
        using var nonGeneric = new BlogContext(builder.Options);

        Assert.Same(ComposedDialect.Instance, generic.GetService<IAdoNetDialect>());
        Assert.Same(ComposedDialect.Instance, nonGeneric.GetService<IAdoNetDialect>());
    }

    // Raw ADO.NET objects don't leave the library (REVIEW_POLICY 4D), except where docs/PUBLIC_API.md records why.
    [Fact]
    public void Public_api_hands_out_no_raw_ado_net_objects_but_the_recorded_ones()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src"));
        string[] recorded =
        [
            "pengdows.efcore.adonet.Bulk.BulkLoad.Connection.get -> System.Data.Common.DbConnection!",
            "pengdows.efcore.adonet.Bulk.BulkLoad.Transaction.get -> System.Data.Common.DbTransaction!",
            "abstract pengdows.efcore.adonet.Testing.ProviderFixture.Factory.get -> System.Data.Common.DbProviderFactory!",
            "abstract pengdows.efcore.adonet.Testing.AdoNetEntryPoints.Factory.get -> System.Data.Common.DbProviderFactory!"
        ];

        var handedOut = Directory.EnumerateFiles(src, "PublicAPI.*.txt", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines)
            .Where(l => l.Contains(" -> System.Data.", StringComparison.Ordinal)
                && System.Text.RegularExpressions.Regex.IsMatch(l, @"-> System\.Data\.(Common\.)?(I?Db\w*|DbDataSource)[!?]?$"))
            .Except(recorded)
            .ToList();

        Assert.True(Directory.Exists(src), src);
        Assert.Empty(handedOut);
    }

    [Fact]
    public void Core_source_names_no_database()
    {
        const string BehaviourReference = "Behaviour reference (not copied): https://github.com/";
        var core = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/pengdows.efcore.adonet"));
        string[] names = ["SQL Server", "T-SQL", "PostgreSQL", "Npgsql", "MySQL", "MariaDB", "SQLite", "DuckDB", "Oracle", "SqlClient", "MySqlConnector"];

        var named = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            // Attribution of code adapted from EF's providers, and references to the official code a rewrite matches, name
            // where they point.
            .Where(l => !l.Text.Contains("(MIT, .NET Foundation)", StringComparison.Ordinal)
                && !l.Text.Contains("(PostgreSQL License)", StringComparison.Ordinal)
                && !l.Text.Contains(BehaviourReference, StringComparison.Ordinal))
            .Where(l => names.Any(n => l.Text.Contains(n, StringComparison.Ordinal)))
            .Select(l => $"{l.File}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(Directory.Exists(core), core);
        Assert.Empty(named);
    }

    [Fact]
    public void Core_depends_on_no_database_driver()
    {
        var references = typeof(IAdoNetDialect).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.All(references, name => Assert.True(
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            || name.StartsWith("System", StringComparison.Ordinal)
            || name == "netstandard",
            $"Core references '{name}'; only EF Core, Microsoft.Extensions and the BCL are allowed."));
    }

    [Fact]
    public void Detection_only_knows_registered_dialects()
    {
        var factory = new UnknownFactory();
        Assert.Null(AdoNetDialects.Find(factory));

        AdoNetDialects.Register<UnknownFactory>(ComposedDialect.Instance);

        Assert.Same(ComposedDialect.Instance, AdoNetDialects.Find(factory));
        using var context = new BlogContext(
            new DbContextOptionsBuilder<BlogContext>().UseAdoNet(factory, "Data Source=:memory:").AllowManyDialects().Options);
        Assert.Same(ComposedDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void Equal_dialects_share_the_internal_service_provider()
    {
        // Singleton services are shared exactly when the internal service provider is.
        using var first = Context(new ConfigurableDialect(compatibilityLevel: 150));
        using var second = Context(new ConfigurableDialect(compatibilityLevel: 150));
        using var other = Context(new ConfigurableDialect(compatibilityLevel: 100));

        Assert.Same(first.GetService<IQuerySqlGeneratorFactory>(), second.GetService<IQuerySqlGeneratorFactory>());
        Assert.NotSame(first.GetService<IQuerySqlGeneratorFactory>(), other.GetService<IQuerySqlGeneratorFactory>());
    }

    [Fact]
    public void Dialects_that_differ_get_their_own_services()
    {
        using var older = Context(new ConfigurableDialect(compatibilityLevel: 100));
        using var newer = Context(new ConfigurableDialect(compatibilityLevel: 150));

        Assert.Equal(100, ((ConfigurableDialect)older.GetService<IAdoNetDialect>()).CompatibilityLevel);
        Assert.Equal(150, ((ConfigurableDialect)newer.GetService<IAdoNetDialect>()).CompatibilityLevel);
    }

    [Fact]
    public void Positional_parameters_are_rejected()
        => Assert.Throws<NotSupportedException>(() => Context(PositionalDialect.Instance).Model);

    [Fact]
    public void Select_after_without_multiple_statements_is_rejected()
        => Assert.Throws<NotSupportedException>(() => Context(SingleStatementSelectAfterDialect.Instance).Model);

    private sealed class ComposedDialect : AdoNetDialect
    {
        public static readonly ComposedDialect Instance = new();

        public override string Name => "Composed";
        public override ISqlDialect Sql { get; } = new ComposedSql();

        private sealed class ComposedSql : AnsiSqlDialect
        {
            public override string IdentifierQuotePrefix => "[";
            public override string IdentifierQuoteSuffix => "]";
            public override string ParameterMarker => ":";
            public override string TrueLiteral => "1";
            public override string FalseLiteral => "0";
            public override PagingSyntax Paging => PagingSyntax.TopOrOffsetFetch;
            public override BooleanSyntax Booleans => BooleanSyntax.BitValues;
        }
    }

    private sealed class LiteralPagingDialect : AdoNetDialect
    {
        public static readonly LiteralPagingDialect Instance = new();

        public override string Name => "Literal paging";
        public override ISqlDialect Sql { get; } = new LiteralPagingSql();

        private sealed class LiteralPagingSql : AnsiSqlDialect
        {
            public override PagingSyntax Paging => PagingSyntax.OffsetFetch.WithLiteralValues();
        }
    }

    private sealed class ServiceDialect : AdoNetDialect
    {
        public static readonly ServiceDialect Instance = new();

        public override string Name => "Services";

        public override void ConfigureServices(IServiceCollection services)
            => new EntityFrameworkRelationalServicesBuilder(services).TryAdd<IHistoryRepository, HistoryRepository>();

        public sealed class HistoryRepository(HistoryRepositoryDependencies dependencies)
            : Microsoft.EntityFrameworkCore.Migrations.HistoryRepository(dependencies)
        {
            public override LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Connection;
            protected override string ExistsSql => "";
            protected override bool InterpretExistsResult(object? value) => false;
            public override string GetCreateIfNotExistsScript() => "";
            public override string GetBeginIfNotExistsScript(string migrationId) => "";
            public override string GetBeginIfExistsScript(string migrationId) => "";
            public override string GetEndIfScript() => "";
            public override IMigrationsDatabaseLock AcquireDatabaseLock() => throw new NotSupportedException();

            public override Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }
    }

    /// <summary>A dialect with settings, like a compatibility level; value equality lets equal settings share services.</summary>
    private sealed class ConfigurableDialect(int compatibilityLevel) : AdoNetDialect
    {
        public int CompatibilityLevel { get; } = compatibilityLevel;
        public override string Name => $"Configurable {CompatibilityLevel}";

        public override bool Equals(object? obj)
            => obj is ConfigurableDialect other && other.CompatibilityLevel == CompatibilityLevel;

        public override int GetHashCode()
            => CompatibilityLevel;
    }

    private sealed class PositionalDialect : AdoNetDialect
    {
        public static readonly PositionalDialect Instance = new();

        public override string Name => "Positional";
        public override ISqlDialect Sql { get; } = new PositionalSql();

        private sealed class PositionalSql : AnsiSqlDialect
        {
            public override string ParameterMarker => "";
        }
    }

    private sealed class SingleStatementSelectAfterDialect : AdoNetDialect
    {
        public static readonly SingleStatementSelectAfterDialect Instance = new();

        public override string Name => "Single statement";
        public override ISqlDialect Sql { get; } = new SelectAfterSql();

        private sealed class SelectAfterSql : AnsiSqlDialect
        {
            public override KeyRetrieval Keys => KeyRetrieval.SelectAfter("last_id()", "row_count()");
        }
    }

    private sealed class UnknownFactory : DbProviderFactory
    {
        public override DbConnection CreateConnection()
            => SqliteFactory.Instance.CreateConnection();
    }
}

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
            .UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", o => o.UseDialect(dialect));
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
            new DbContextOptionsBuilder<BlogContext>().UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", ComposedDialect.Instance).Options);
        var builder = new DbContextOptionsBuilder<BlogContext>();
        ((DbContextOptionsBuilder)builder).UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", ComposedDialect.Instance, o => o.MaxBatchSize(7));
        using var nonGeneric = new BlogContext(builder.Options);

        Assert.Same(ComposedDialect.Instance, generic.GetService<IAdoNetDialect>());
        Assert.Same(ComposedDialect.Instance, nonGeneric.GetService<IAdoNetDialect>());
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
        Assert.Same(AdoNetDialect.Ansi, AdoNetDialects.Find(factory));

        AdoNetDialects.Register<UnknownFactory>(ComposedDialect.Instance);

        Assert.Same(ComposedDialect.Instance, AdoNetDialects.Find(factory));
        using var context = new BlogContext(
            new DbContextOptionsBuilder<BlogContext>().UseAdoNet(factory, "Data Source=:memory:").Options);
        Assert.Same(ComposedDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void Equal_dialects_share_the_internal_service_provider()
    {
        // EF throws after 20 internal service providers when this warning is configured to throw.
        for (var i = 0; i < 30; i++)
        {
            using var context = Context(
                new ConfigurableDialect(compatibilityLevel: 150),
                b => b.ConfigureWarnings(w => w.Throw(CoreEventId.ManyServiceProvidersCreatedWarning)));

            Assert.NotNull(context.Model);
        }
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

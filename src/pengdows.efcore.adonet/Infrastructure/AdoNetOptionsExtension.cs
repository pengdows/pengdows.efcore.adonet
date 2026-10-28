using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.Infrastructure;

public class AdoNetOptionsExtension : RelationalOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;
    private DbProviderFactory? _providerFactory;
    private DbDataSource? _dataSource;
    private IAdoNetDialect? _dialect;
    private IAdoNetDialect? _registeredDialect;
    private DbProviderFactory? _sourceFactory;
    private bool _dataSourceProbed;
    private QuerySplittingBehavior? _adoNetQuerySplittingBehavior;
    private bool _reverseNullOrdering;

    public AdoNetOptionsExtension()
    {
    }

    protected AdoNetOptionsExtension(AdoNetOptionsExtension copyFrom)
        : base(copyFrom)
    {
        _providerFactory = copyFrom._providerFactory;
        _dataSource = copyFrom._dataSource;
        _dialect = copyFrom._dialect;
        _registeredDialect = copyFrom._registeredDialect;
        _sourceFactory = copyFrom._sourceFactory;
        _dataSourceProbed = copyFrom._dataSourceProbed;
        _adoNetQuerySplittingBehavior = copyFrom._adoNetQuerySplittingBehavior;
        _reverseNullOrdering = copyFrom._reverseNullOrdering;
    }

    public override DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    /// <summary>The ADO.NET provider's query splitting setting, including the absence of an explicit setting.</summary>
    public override QuerySplittingBehavior? QuerySplittingBehavior
        => _adoNetQuerySplittingBehavior;

    public override RelationalOptionsExtension WithUseQuerySplittingBehavior(QuerySplittingBehavior querySplittingBehavior)
    {
        var clone = (AdoNetOptionsExtension)base.WithUseQuerySplittingBehavior(querySplittingBehavior);
        clone._adoNetQuerySplittingBehavior = querySplittingBehavior;
        return clone;
    }

    /// <summary>Returns a copy with no context-level query splitting behavior, allowing EF's warning path to run.</summary>
    internal AdoNetOptionsExtension WithoutQuerySplittingBehavior()
    {
        var clone = (AdoNetOptionsExtension)Clone();
        clone._adoNetQuerySplittingBehavior = null;
        return clone;
    }

    /// <summary>
    ///     Whether every ORDER BY says where nulls sort (<c>NULLS FIRST</c> ascending, <c>NULLS LAST</c> descending), so they sort
    ///     as in .NET and SQL Server (Behaviour reference (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerQuerySqlGenerator.cs) whatever the database's own default is. Not public, as in the providers it follows: the
    ///     indexes would need to be created to match for such an order to use them.
    /// </summary>
    internal bool ReverseNullOrdering
        => _reverseNullOrdering;

    internal AdoNetOptionsExtension WithReverseNullOrdering(bool reverseNullOrdering)
    {
        var clone = (AdoNetOptionsExtension)Clone();
        clone._reverseNullOrdering = reverseNullOrdering;
        return clone;
    }

    /// <summary>Used with <see cref="RelationalOptionsExtension.ConnectionString" /> to create connections.</summary>
    internal DbProviderFactory? ProviderFactory
        => _providerFactory;

    /// <summary>When set, connections come from here; the caller owns its lifetime (and its pool).</summary>
    internal DbDataSource? DataSource
        => _dataSource;

    /// <summary>The dialect from <c>UseDialect</c>, else the one registered for the configured factory.</summary>
    public virtual IAdoNetDialect Dialect
        => ChosenDialect
            ?? throw new InvalidOperationException(
                $"No dialect is registered for {(_sourceFactory == null ? "this connection's provider" : $"'{_sourceFactory.GetType().Name}'")}. "
                + "Choose one with UseDialect (AdoNetDialect.Ansi for ISO SQL), or configure through a dialect package's UseAdoNet... method.");

    private IAdoNetDialect? ChosenDialect
        => _dialect ?? _registeredDialect ?? DataSourceDialect();

    // A data source names no provider: only when no dialect was chosen is one connection created (not opened) to ask.
    private IAdoNetDialect? DataSourceDialect()
    {
        if ((_dataSource == null) || _dataSourceProbed)
        {
            return _registeredDialect;
        }

        using (var probe = _dataSource.CreateConnection())
        {
            WithSourceFactory(DbProviderFactories.GetFactory(probe));
        }

        _dataSourceProbed = true;
        return _registeredDialect;
    }

    protected override RelationalOptionsExtension Clone()
        => new AdoNetOptionsExtension(this);

    // Each source replaces any other, as configuring a provider again does in EF.
    public virtual AdoNetOptionsExtension WithProviderFactory(DbProviderFactory providerFactory)
    {
        var clone = (AdoNetOptionsExtension)base.WithConnection(null);
        clone._providerFactory = providerFactory;
        clone._dataSource = null;
        return clone.WithSourceFactory(providerFactory);
    }

    public virtual AdoNetOptionsExtension WithDataSource(DbDataSource dataSource)
    {
        var clone = (AdoNetOptionsExtension)base.WithConnection(null).WithConnectionString(null);
        clone._dataSource = dataSource;
        clone._providerFactory = null;
        clone._sourceFactory = null;
        clone._registeredDialect = null;
        clone._dataSourceProbed = false;
        return clone;
    }

    public override RelationalOptionsExtension WithConnection(DbConnection? connection)
        => WithConnection(connection, owned: false);

    public override RelationalOptionsExtension WithConnection(DbConnection? connection, bool owned)
    {
        var clone = (AdoNetOptionsExtension)base.WithConnection(connection, owned).WithConnectionString(null);
        clone._providerFactory = null;
        clone._dataSource = null;
        return connection == null ? clone : clone.WithSourceFactory(DbProviderFactories.GetFactory(connection));
    }

    private AdoNetOptionsExtension WithSourceFactory(DbProviderFactory? factory)
    {
        _sourceFactory = factory;
        _registeredDialect = AdoNetDialects.Find(factory);
        return this;
    }

    public virtual AdoNetOptionsExtension WithDialect(IAdoNetDialect dialect)
    {
        var clone = (AdoNetOptionsExtension)Clone();
        clone._dialect = dialect;
        return clone;
    }

    public override void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkAdoNet(Dialect);

    public override void Validate(IDbContextOptions options)
    {
        base.Validate(options);

        // The dialect partitions the service provider, so this runs once per distinct dialect.
        Validate(Dialect);

        if (_reverseNullOrdering && !Dialect.Sql.SupportsNullOrderingClauses)
        {
            throw new NotSupportedException(
                $"Dialect '{Dialect.Name}' does not accept NULLS FIRST / NULLS LAST in ORDER BY, which ReverseNullOrdering writes.");
        }
    }

    /// <summary>Combinations of dialect parts the provider cannot work with.</summary>
    public static void Validate(IAdoNetDialect dialect)
    {
        if (string.IsNullOrEmpty(dialect.Sql.ParameterMarker))
        {
            throw new NotSupportedException(
                $"Dialect '{dialect.Name}': positional ('?') parameters are not supported; EF Core reuses named parameters within a statement.");
        }

        foreach (var (member, template, arguments) in dialect.Sql.Functions.Templates())
        {
            if ((template != null) && (Query.SqlTemplate.Validate(template, arguments) is { } problem))
            {
                throw new InvalidOperationException($"Dialect '{dialect.Name}': FunctionSyntax.{member}: {problem}");
            }
        }

        if (dialect.Sql.Keys is SelectAfterKeyRetrieval && !dialect.Capabilities.SupportsMultipleStatementsPerCommand)
        {
            throw new NotSupportedException(
                $"Dialect '{dialect.Name}': SelectAfter key retrieval needs multiple statements per command.");
        }
    }

    private sealed class ExtensionInfo(AdoNetOptionsExtension extension) : RelationalExtensionInfo(extension)
    {
        private new AdoNetOptionsExtension Extension
            => (AdoNetOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider
            => true;

        // The dialect is consumed by singleton services and registers services of its own, so it partitions the internal
        // service provider. Equality is the dialect's: reference equality unless it defines value equality.
        public override int GetServiceProviderHashCode()
            => HashCode.Combine(Extension.ChosenDialect, Extension._reverseNullOrdering);

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo otherInfo
                && Equals(Extension.ChosenDialect, otherInfo.Extension.ChosenDialect)
                && Extension._reverseNullOrdering == otherInfo.Extension._reverseNullOrdering;

        public override string LogFragment
            => base.LogFragment + "Dialect=" + (Extension.ChosenDialect?.Name ?? "(none)") + " ";

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["AdoNet:Dialect"] = (Extension.ChosenDialect?.GetHashCode() ?? 0).ToString(CultureInfo.InvariantCulture);
    }
}

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
    }

    public override DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    /// <summary>Used with <see cref="RelationalOptionsExtension.ConnectionString" /> to create connections.</summary>
    public virtual DbProviderFactory? ProviderFactory
        => _providerFactory;

    /// <summary>When set, connections come from here; the caller owns its lifetime (and its pool).</summary>
    public virtual DbDataSource? DataSource
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
    }

    /// <summary>Combinations of dialect parts the provider cannot work with.</summary>
    public static void Validate(IAdoNetDialect dialect)
    {
        if (string.IsNullOrEmpty(dialect.Sql.ParameterMarker))
        {
            throw new NotSupportedException(
                $"Dialect '{dialect.Name}': positional ('?') parameters are not supported; EF Core reuses named parameters within a statement.");
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
            => Extension.ChosenDialect?.GetHashCode() ?? 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo otherInfo
                && Equals(Extension.ChosenDialect, otherInfo.Extension.ChosenDialect);

        public override string LogFragment
            => base.LogFragment + "Dialect=" + (Extension.ChosenDialect?.Name ?? "(none)") + " ";

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["AdoNet:Dialect"] = (Extension.ChosenDialect?.GetHashCode() ?? 0).ToString(CultureInfo.InvariantCulture);
    }
}

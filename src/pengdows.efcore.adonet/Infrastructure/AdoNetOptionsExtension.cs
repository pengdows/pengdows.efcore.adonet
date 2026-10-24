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
    private IAdoNetDialect _dialect = AdoNetDialect.Ansi;

    public AdoNetOptionsExtension()
    {
    }

    protected AdoNetOptionsExtension(AdoNetOptionsExtension copyFrom)
        : base(copyFrom)
    {
        _providerFactory = copyFrom._providerFactory;
        _dataSource = copyFrom._dataSource;
        _dialect = copyFrom._dialect;
    }

    public override DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    /// <summary>Used with <see cref="RelationalOptionsExtension.ConnectionString" /> to create connections.</summary>
    public virtual DbProviderFactory? ProviderFactory
        => _providerFactory;

    /// <summary>When set, connections come from here; the caller owns its lifetime (and its pool).</summary>
    public virtual DbDataSource? DataSource
        => _dataSource;

    public virtual IAdoNetDialect Dialect
        => _dialect;

    protected override RelationalOptionsExtension Clone()
        => new AdoNetOptionsExtension(this);

    public virtual AdoNetOptionsExtension WithProviderFactory(DbProviderFactory providerFactory)
    {
        var clone = (AdoNetOptionsExtension)Clone();
        clone._providerFactory = providerFactory;
        return clone;
    }

    public virtual AdoNetOptionsExtension WithDataSource(DbDataSource dataSource)
    {
        var clone = (AdoNetOptionsExtension)Clone();
        clone._dataSource = dataSource;
        return clone;
    }

    public virtual AdoNetOptionsExtension WithDialect(IAdoNetDialect dialect)
    {
        var clone = (AdoNetOptionsExtension)Clone();
        clone._dialect = dialect;
        return clone;
    }

    public override void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkAdoNet(_dialect);

    public override void Validate(IDbContextOptions options)
    {
        base.Validate(options);

        // The dialect partitions the service provider, so this runs once per distinct dialect.
        Validate(_dialect);
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
            => Extension._dialect.GetHashCode();

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo otherInfo
                && Extension._dialect.Equals(otherInfo.Extension._dialect);

        public override string LogFragment
            => base.LogFragment + "Dialect=" + Extension._dialect.Name + " ";

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["AdoNet:Dialect"] = Extension._dialect.GetHashCode().ToString(CultureInfo.InvariantCulture);
    }
}

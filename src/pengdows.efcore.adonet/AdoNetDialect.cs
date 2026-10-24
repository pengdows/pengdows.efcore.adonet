using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet;

/// <summary>Base for dialects: ANSI parts throughout, so a dialect overrides only what its database does differently.</summary>
public abstract class AdoNetDialect : IAdoNetDialect
{
    public static IAdoNetDialect Ansi { get; } = new AnsiDialect();

    public abstract string Name { get; }

    public virtual ISqlDialect Sql
        => AnsiSqlDialect.Instance;

    public virtual ITypeMappingDialect TypeMappings
        => AnsiTypeMappings.Instance;

    public virtual IDatabaseCapabilities Capabilities
        => DatabaseCapabilities.Default;

    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    public override string ToString()
        => Name;
}

public sealed class AnsiDialect : AdoNetDialect
{
    public override string Name => "ANSI";
}

/// <summary>ISO SQL; subclass and override what differs.</summary>
public class AnsiSqlDialect : ISqlDialect
{
    public static readonly AnsiSqlDialect Instance = new();

    public virtual string IdentifierQuotePrefix => "\"";
    public virtual string IdentifierQuoteSuffix => "\"";
    public virtual bool SupportsSchemas => true;
    public virtual string ParameterMarker => "@";
    public virtual bool ParameterNameIncludesMarker => true;
    public virtual string TrueLiteral => "TRUE";
    public virtual string FalseLiteral => "FALSE";
    public virtual string LongCountFunction => "COUNT";
    public virtual PagingSyntax Paging => PagingSyntax.OffsetFetch;
    public virtual BooleanSyntax Booleans => BooleanSyntax.Native;
    public virtual KeyRetrieval Keys => KeyRetrieval.Returning;
    public virtual DmlSyntax Dml => DmlSyntax.Standard;

    public virtual string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
}

/// <summary>ISO SQL type names; subclass and override <see cref="GetStoreType" /> for what differs.</summary>
public class AnsiTypeMappings : ITypeMappingDialect
{
    public static readonly AnsiTypeMappings Instance = new();

    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "BOOLEAN",
        [typeof(byte)] = "SMALLINT",
        [typeof(short)] = "SMALLINT",
        [typeof(int)] = "INTEGER",
        [typeof(long)] = "BIGINT",
        [typeof(float)] = "REAL",
        [typeof(double)] = "DOUBLE PRECISION",
        [typeof(decimal)] = "DECIMAL(18,2)",
        [typeof(string)] = "VARCHAR",
        [typeof(DateTime)] = "TIMESTAMP",
        [typeof(DateTimeOffset)] = "TIMESTAMP WITH TIME ZONE",
        [typeof(DateOnly)] = "DATE",
        [typeof(TimeOnly)] = "TIME",
        [typeof(TimeSpan)] = "INTERVAL",
        [typeof(Guid)] = "UUID",
        [typeof(byte[])] = "VARBINARY"
    };

    /// <summary>CLR types the provider maps; <see cref="ITypeMappingDialect.GetStoreType" /> supplies each one's store type.</summary>
    public static IReadOnlyCollection<Type> SupportedClrTypes
        => StoreTypes.Keys;

    public virtual string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType);

    public virtual string? GetStoreType(Type clrType, int? size, int? precision, int? scale)
        => clrType == typeof(string) && size != null ? $"VARCHAR({size})"
            : clrType == typeof(byte[]) && size != null ? $"VARBINARY({size})"
            : clrType == typeof(decimal) && precision != null ? $"DECIMAL({precision},{scale ?? 0})"
            : null;
}

public sealed record DatabaseCapabilities(
    bool SupportsMultipleStatementsPerCommand = false,
    int DefaultMaxBatchSize = 1,
    int MaxParametersPerCommand = 999)
    : IDatabaseCapabilities
{
    /// <summary>One statement per command.</summary>
    public static readonly DatabaseCapabilities Default = new();
}

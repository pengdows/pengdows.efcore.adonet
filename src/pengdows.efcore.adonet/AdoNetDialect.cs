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
    public virtual string StringLiteral(string value)
        => "'" + value.Replace("'", "''") + "'";

    public virtual string TrueLiteral => "TRUE";
    public virtual string FalseLiteral => "FALSE";
    public virtual string LongCountFunction => "COUNT";
    public virtual PagingSyntax Paging => PagingSyntax.OffsetFetch;
    public virtual BooleanSyntax Booleans => BooleanSyntax.Native;
    public virtual KeyRetrieval Keys => KeyRetrieval.Returning;
    public virtual DmlSyntax Dml => DmlSyntax.Standard;
    public virtual FunctionSyntax Functions => FunctionSyntax.Iso;
    public virtual bool PreferExistsToIn => false;
    public virtual string DefaultValues => "DEFAULT VALUES";
    public virtual string? CurrentSchema => null;
    public virtual SavepointSyntax? Savepoints => SavepointSyntax.Iso;

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

    // ISO SQL type names and their common spellings; each holds these CLR types without conversion, the default first.
    private static readonly Dictionary<string, Type[]> ClrTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INTEGER"] = [typeof(int)], ["INT"] = [typeof(int)], ["INT4"] = [typeof(int)],
        ["SMALLINT"] = [typeof(short)], ["INT2"] = [typeof(short)],
        ["BIGINT"] = [typeof(long)], ["INT8"] = [typeof(long)],
        ["REAL"] = [typeof(float)], ["FLOAT4"] = [typeof(float)],
        ["DOUBLE PRECISION"] = [typeof(double)], ["DOUBLE"] = [typeof(double)], ["FLOAT"] = [typeof(double)], ["FLOAT8"] = [typeof(double)],
        ["DECIMAL"] = [typeof(decimal)], ["NUMERIC"] = [typeof(decimal)], ["DEC"] = [typeof(decimal)],
        ["BOOLEAN"] = [typeof(bool)], ["BOOL"] = [typeof(bool)],
        ["CHARACTER"] = [typeof(string)], ["CHAR"] = [typeof(string)], ["CHARACTER VARYING"] = [typeof(string)],
        ["CHAR VARYING"] = [typeof(string)], ["VARCHAR"] = [typeof(string)], ["NATIONAL CHARACTER"] = [typeof(string)],
        ["NATIONAL CHAR"] = [typeof(string)], ["NCHAR"] = [typeof(string)], ["NATIONAL CHARACTER VARYING"] = [typeof(string)],
        ["NATIONAL CHAR VARYING"] = [typeof(string)], ["NCHAR VARYING"] = [typeof(string)], ["NVARCHAR"] = [typeof(string)],
        ["CHARACTER LARGE OBJECT"] = [typeof(string)], ["CLOB"] = [typeof(string)], ["NCLOB"] = [typeof(string)], ["TEXT"] = [typeof(string)],
        ["BINARY"] = [typeof(byte[])], ["BINARY VARYING"] = [typeof(byte[])], ["VARBINARY"] = [typeof(byte[])],
        ["BINARY LARGE OBJECT"] = [typeof(byte[])], ["BLOB"] = [typeof(byte[])],
        ["TIMESTAMP"] = [typeof(DateTime)], ["TIMESTAMP WITHOUT TIME ZONE"] = [typeof(DateTime)],
        ["TIMESTAMP WITH TIME ZONE"] = [typeof(DateTimeOffset)],
        ["DATE"] = [typeof(DateOnly), typeof(DateTime)],
        ["TIME"] = [typeof(TimeOnly), typeof(TimeSpan)], ["TIME WITHOUT TIME ZONE"] = [typeof(TimeOnly), typeof(TimeSpan)],
        ["INTERVAL"] = [typeof(TimeSpan)],
        ["UUID"] = [typeof(Guid)]
    };

    /// <summary>CLR types the provider maps; <see cref="ITypeMappingDialect.GetStoreType" /> supplies each one's store type.</summary>
    public static IReadOnlyCollection<Type> SupportedClrTypes
        => StoreTypes.Keys;

    public virtual string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType);

    public virtual int? StringParameterSize(bool unicode)
        => null;

    public virtual string FormatLiteral(object value, string storeType, string literal)
        => literal;

    public virtual IReadOnlyList<Type> GetClrTypes(string storeTypeBase)
        => ClrTypes.TryGetValue(storeTypeBase, out var types) ? types : [];

    public virtual string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => facets switch
        {
            { Size: { } size } when clrType == typeof(string) => facets.FixedLength ? $"CHAR({size})" : $"VARCHAR({size})",
            { Size: { } size } when clrType == typeof(byte[]) => facets.FixedLength ? $"BINARY({size})" : $"VARBINARY({size})",
            { Precision: { } precision } when clrType == typeof(decimal) => $"DECIMAL({precision},{facets.Scale ?? 0})",
            _ => null
        };
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

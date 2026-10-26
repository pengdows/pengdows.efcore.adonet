using System.Globalization;
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

internal sealed class AnsiDialect : AdoNetDialect
{
    public override string Name => "ANSI";
}

/// <summary>ISO SQL; subclass and override what differs.</summary>
public class AnsiSqlDialect : ISqlDialect
{
    public static readonly AnsiSqlDialect Instance = new();

    public virtual string IdentifierQuotePrefix => "\"";
    public virtual string IdentifierQuoteSuffix => "\"";

    public virtual bool RequiresQuoting(string identifier)
        => true;
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

    // ISO SQL has no CREATE DATABASE: each dialect says how its databases are made.
    public virtual DatabaseLifecycle? Databases => null;
    public virtual bool RowValues => true;
    public virtual bool LimitInPredicateSubqueries => true;
    public virtual LateralJoinSyntax? LateralJoins => LateralJoinSyntax.Iso;

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
        => value is TimeSpan span ? IntervalLiteral(span) : literal;

    // ISO's day-time interval. Its defaults hold 2 digits of days and 6 of fractional seconds; a value needing more names
    // DAY(8) (TimeSpan's largest day count) or SECOND(7) (its 100-nanosecond ticks). The magnitude is unsigned, as
    // TimeSpan.MinValue has no positive counterpart.
    private static string IntervalLiteral(TimeSpan span)
    {
        var magnitude = span.Ticks < 0 ? (ulong)(-(span.Ticks + 1)) + 1 : (ulong)span.Ticks;
        var days = magnitude / TimeSpan.TicksPerDay;
        var time = magnitude % TimeSpan.TicksPerDay;
        var seconds = time / TimeSpan.TicksPerSecond;
        var fraction = time % TimeSpan.TicksPerSecond;
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{(span.Ticks < 0 ? "-" : "")}{days} {seconds / 3600:00}:{(seconds / 60) % 60:00}:{seconds % 60:00}");
        var fractionDigits = fraction.ToString("0000000", CultureInfo.InvariantCulture).TrimEnd('0');
        if (fractionDigits.Length > 0)
        {
            text += "." + fractionDigits;
        }

        var day = days > 99 ? "DAY(8)" : "DAY";
        var second = fractionDigits.Length > 6 ? "SECOND(7)" : "SECOND";
        return $"INTERVAL '{text}' {day} TO {second}";
    }

    public virtual string CastTarget(string storeType)
        => storeType;

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

/// <summary>
///     ISO SQL's answers to <see cref="IDatabaseCapabilities" />. A dialect sets what its database does differently, in an
///     initializer or by overriding in a subclass.
/// </summary>
public record DatabaseCapabilities : IDatabaseCapabilities
{
    /// <summary>ISO SQL, one statement per command.</summary>
    public static readonly DatabaseCapabilities Default = new();

    public virtual bool SupportsMultipleStatementsPerCommand { get; init; }
    public virtual int DefaultMaxBatchSize { get; init; } = 1;
    public virtual int MaxParametersPerCommand { get; init; } = 999;

    /// <summary>128, SQL:2003's limit for an identifier.</summary>
    public virtual int MaxIdentifierLength { get; init; } = 128;

    /// <summary>False: enlisting in a <c>TransactionScope</c> is the driver's, which says so in its own dialect.</summary>
    public virtual bool SupportsAmbientTransactions { get; init; }

    /// <summary>False, the safe assumption: a split query is buffered.</summary>
    public virtual bool SupportsMultipleActiveReaders { get; init; }

    /// <summary>
    ///     What the driver reports as transient (<see cref="System.Data.Common.DbException.IsTransient" />), or a timeout; a
    ///     dialect whose driver doesn't report it adds the driver's own transient errors.
    /// </summary>
    public virtual bool IsTransient(Exception exception)
        => exception is System.Data.Common.DbException { IsTransient: true } or TimeoutException;
}

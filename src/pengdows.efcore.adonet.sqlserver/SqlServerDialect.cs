using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     SQL Server. Identifiers are bracketed, as the official provider writes them. BIT is not a
///     predicate, paging uses TOP or OFFSET/FETCH, and keys come from <c>SCOPE_IDENTITY()</c> so tables with triggers work.
/// </summary>
public sealed class SqlServerDialect : AdoNetDialect
{
    public static readonly SqlServerDialect Instance = new();

    public override string Name => "SQL Server";
    public override ISqlDialect Sql { get; } = new SqlServerSql();
    public override ITypeMappingDialect TypeMappings { get; } = new SqlServerTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 42, MaxParametersPerCommand: 2000);

    public override void ConfigureServices(IServiceCollection services)
    {
        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, SqlServerMigrationsSqlGenerator>()
            .TryAdd<IRelationalQueryStringFactory, SqlServerQueryStringFactory>();
        services.TryAddScoped<IAdoNetBulkLoader, SqlServerBulkLoader>();
    }
}

public sealed class SqlServerSql : AnsiSqlDialect
{
    public override string IdentifierQuotePrefix => "[";
    public override string IdentifierQuoteSuffix => "]";
    public override string TrueLiteral => "CAST(1 AS bit)";
    public override string FalseLiteral => "CAST(0 AS bit)";
    public override string LongCountFunction => "COUNT_BIG";
    public override PagingSyntax Paging => PagingSyntax.TopOrOffsetFetch;
    public override BooleanSyntax Booleans => BooleanSyntax.BitValuesWithBitwiseOperators;
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("SCOPE_IDENTITY()", "@@ROWCOUNT");
    public override DmlSyntax Dml => DmlSyntax.TargetAliasFrom;
    public override FunctionSyntax Functions { get; } = new SqlServerFunctions();
    public override bool PreferExistsToIn => true;

    public override string StringLiteral(string value)
        => "N" + base.StringLiteral(value);
}

/// <summary>
///     As the official provider writes them. LEN ignores trailing spaces, as SQL Server's string comparison does; it returns
///     bigint for MAX types, hence the CAST. CHARINDEX finds an empty string nowhere; .NET finds it at 0.
/// </summary>
public sealed class SqlServerFunctions : FunctionSyntax
{
    public override string Concat => "{0} + {1}";
    public override string? TrimStart => "LTRIM({0})";
    public override string? TrimEnd => "RTRIM({0})";
    public override string? Length => "CAST(LEN({0}) AS int)";
    public override string? Substring => "SUBSTRING({0}, {1}, {2})";
    public override string? SubstringFrom => "SUBSTRING({0}, {1}, LEN({0}))";
    public override string? Position => "CAST(CASE WHEN DATALENGTH({0}) = 0 THEN 1 ELSE CHARINDEX({0}, {1}) END AS int)";
    public override string? Round => "ROUND({0}, 0)";
    public override string? Year => "DATEPART(year, {0})";
    public override string? Month => "DATEPART(month, {0})";
    public override string? Day => "DATEPART(day, {0})";
    public override string? Hour => "DATEPART(hour, {0})";
    public override string? Minute => "DATEPART(minute, {0})";
    public override string? Second => "DATEPART(second, {0})";
    public override string LikeSpecialCharacters => "%_[";
}

public sealed class SqlServerTypeMappings : AnsiTypeMappings
{
    // As the official provider names them.
    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "bit",
        [typeof(byte)] = "tinyint",
        [typeof(short)] = "smallint",
        [typeof(int)] = "int",
        [typeof(long)] = "bigint",
        [typeof(float)] = "real",
        [typeof(double)] = "float",
        [typeof(decimal)] = "decimal(18,2)",
        [typeof(string)] = "nvarchar(max)",
        [typeof(DateTime)] = "datetime2",
        [typeof(DateTimeOffset)] = "datetimeoffset",
        [typeof(DateOnly)] = "date",
        [typeof(TimeOnly)] = "time",
        [typeof(TimeSpan)] = "time",
        [typeof(Guid)] = "uniqueidentifier",
        [typeof(byte[])] = "varbinary(max)"
    };

    // SQL Server's own names; "timestamp" is rowversion here, not ISO's date and time.
    private static readonly Dictionary<string, Type[]> ClrTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bit"] = [typeof(bool)],
        ["tinyint"] = [typeof(byte)],
        ["money"] = [typeof(decimal)],
        ["smallmoney"] = [typeof(decimal)],
        ["datetime"] = [typeof(DateTime)],
        ["datetime2"] = [typeof(DateTime)],
        ["smalldatetime"] = [typeof(DateTime)],
        ["datetimeoffset"] = [typeof(DateTimeOffset)],
        ["uniqueidentifier"] = [typeof(Guid)],
        ["ntext"] = [typeof(string)],
        ["xml"] = [typeof(string)],
        ["image"] = [typeof(byte[])],
        ["rowversion"] = [typeof(byte[])],
        ["timestamp"] = [typeof(byte[])]
    };

    public override string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);

    // Literals as the official provider writes them (its SqlServer*TypeMapping classes, MIT, .NET Foundation).
    public override string FormatLiteral(object value, string storeType, string literal)
        => value switch
        {
            short or byte or long or float => $"CAST({literal} AS {storeType})",
            double d => literal.Contains('E') || literal.Contains('e') || double.IsNaN(d) || double.IsInfinity(d) ? literal : literal + "E0",
            DateTime dateTime => Format(dateTime, BaseName(storeType) switch
            {
                "date" => "yyyy-MM-dd",
                "datetime" => "yyyy-MM-ddTHH:mm:ss.fff",
                "smalldatetime" => "yyyy-MM-ddTHH:mm:ss",
                _ => "yyyy-MM-ddTHH:mm:ss" + Fraction('f', Precision(storeType)) + "K"
            }),
            DateOnly date => Format(date, "yyyy-MM-dd"),
            TimeOnly time => Format(time, time.Ticks % TimeSpan.TicksPerSecond == 0 ? @"HH\:mm\:ss" : @"HH\:mm\:ss" + Fraction('F', Precision(storeType), @"\.")),
            TimeSpan span => Format(span, span.Milliseconds == 0 ? @"hh\:mm\:ss" : @"hh\:mm\:ss" + Fraction('F', Precision(storeType), @"\.")),
            DateTimeOffset offset => Format(offset, "yyyy-MM-ddTHH:mm:ss" + Fraction('f', Precision(storeType)) + "zzz"),
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            _ => literal
        };

    private static string BaseName(string storeType)
        => storeType.IndexOf('(') is > 0 and var parenthesis ? storeType[..parenthesis].Trim() : storeType;

    private static int Precision(string storeType)
        => storeType.IndexOf('(') is > 0 and var open && int.TryParse(storeType.AsSpan(open + 1, storeType.Length - open - 2), out var precision)
            && precision is >= 0 and <= 7
                ? precision
                : 7;

    private static string Fraction(char digit, int precision, string separator = ".")
        => precision == 0 ? "" : separator + new string(digit, precision);

    private static string Format(IFormattable value, string format)
        => "'" + value.ToString(format, CultureInfo.InvariantCulture) + "'";

    // Bounded sizes keep one plan for values of any length up to them.
    public override int? StringParameterSize(bool unicode)
        => unicode ? 4000 : 8000;

    public override IReadOnlyList<Type> GetClrTypes(string storeTypeBase)
        => ClrTypes.TryGetValue(storeTypeBase, out var types) ? types : base.GetClrTypes(storeTypeBase);

    public override string? GetStoreType(Type clrType, int? size, int? precision, int? scale, bool fixedLength)
        => clrType == typeof(string) && size != null ? (size <= 4000 ? fixedLength ? $"nchar({size})" : $"nvarchar({size})" : "nvarchar(max)")
            : clrType == typeof(byte[]) && size != null ? (size <= 8000 ? $"varbinary({size})" : "varbinary(max)")
            : clrType == typeof(decimal) && precision != null ? $"decimal({precision},{scale ?? 0})"
            : base.GetStoreType(clrType, size, precision, scale, fixedLength);
}

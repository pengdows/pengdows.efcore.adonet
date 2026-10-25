using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL. Backtick identifiers, a database is the schema, OFFSET needs a LIMIT, and keys come back through
///     <c>LAST_INSERT_ID()</c>. GUIDs are stored as <c>CHAR(36)</c> text.
/// </summary>
public sealed class MySqlDialect : AdoNetDialect
{
    public static readonly MySqlDialect Instance = new(backslashEscapes: true);

    /// <summary>For servers whose sql_mode has NO_BACKSLASH_ESCAPES, where a backslash in a string is just a backslash.</summary>
    public static readonly MySqlDialect NoBackslashEscapes = new(backslashEscapes: false);

    private MySqlDialect(bool backslashEscapes)
    {
        Name = backslashEscapes ? "MySQL" : "MySQL (NO_BACKSLASH_ESCAPES)";
        Sql = new MySqlSql(backslashEscapes);
    }

    public override string Name { get; }
    public override ISqlDialect Sql { get; }
    public override ITypeMappingDialect TypeMappings { get; } = new MySqlTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
        MaxParametersPerCommand = 65535,
        // MySQL's limit for table, column, index and constraint names.
        MaxIdentifierLength = 64
    };

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, MySqlMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, MySqlHistoryRepository>();
}

internal sealed class MySqlSql(bool backslashEscapes = true) : AnsiSqlDialect
{
    public override string IdentifierQuotePrefix => "`";
    public override string IdentifierQuoteSuffix => "`";
    public override bool SupportsSchemas => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset(noLimitValue: "18446744073709551615");
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("LAST_INSERT_ID()", "ROW_COUNT()");
    public override DmlSyntax Dml => DmlSyntax.MultiTable;
    public override FunctionSyntax Functions { get; } = new MySqlFunctions();
    public override string DefaultValues => "() VALUES ()";
    public override string CurrentSchema => "DATABASE()";

    // "This version of MySQL doesn't yet support 'LIMIT & IN/ALL/ANY/SOME subquery'"; a derived table may be paged.
    public override bool LimitInPredicateSubqueries => false;

    // Backslash is an escape character in MySQL string literals, unless the server's sql_mode has NO_BACKSLASH_ESCAPES.
    public override string StringLiteral(string value)
        => base.StringLiteral(backslashEscapes ? value.Replace(@"\", @"\\") : value);

    // INFORMATION_SCHEMA.TABLES spans every database on the server.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = DATABASE()";
}

/// <summary>
///     <c>||</c> means OR. FLOOR and CEILING of a DECIMAL return BIGINT; adding 0.0 keeps the result a decimal. The SECOND
///     part is already whole, and CAST has no INTEGER target.
/// </summary>
internal sealed class MySqlFunctions : FunctionSyntax
{
    // The % operator, as the database's EF Core provider writes it.
    public override string? Modulo => null;

    public override string? Second => "EXTRACT(SECOND FROM {0})";
    public override string Concat => "CONCAT({0}, {1})";
    public override string? Floor => "(FLOOR({0}) + 0.0)";
    public override string? Ceiling => "(CEILING({0}) + 0.0)";
}

internal sealed class MySqlTypeMappings : AnsiTypeMappings
{
    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "TINYINT(1)",
        [typeof(byte)] = "TINYINT UNSIGNED",
        [typeof(float)] = "FLOAT",
        [typeof(double)] = "DOUBLE",
        [typeof(string)] = "LONGTEXT",
        [typeof(DateTime)] = "DATETIME(6)",
        [typeof(TimeOnly)] = "TIME(6)",
        [typeof(TimeSpan)] = "TIME(6)",
        [typeof(Guid)] = "CHAR(36)",
        [typeof(byte[])] = "LONGBLOB"
    };

    private static readonly TimeSpan MaxTime = new(838, 59, 59);

    // TIME spans -838:59:59 to 838:59:59 and counts hours past a day; outside that MySQL would clip the value.
    public override string FormatLiteral(object value, string storeType, string literal)
    {
        if (value is not TimeSpan span)
        {
            return literal;
        }

        if (span.Duration() > MaxTime)
        {
            throw new ArgumentOutOfRangeException(nameof(value), span, $"MySQL's TIME holds -838:59:59 to 838:59:59; {span} is outside it.");
        }

        var magnitude = span.Duration();
        var fraction = (magnitude.Ticks % TimeSpan.TicksPerSecond) == 0 ? "" : magnitude.ToString(@"\.ffffff", CultureInfo.InvariantCulture);
        return "TIME '" + (span < TimeSpan.Zero ? "-" : "")
            + ((long)magnitude.TotalHours).ToString("00", CultureInfo.InvariantCulture)
            + magnitude.ToString(@"\:mm\:ss", CultureInfo.InvariantCulture) + fraction + "'";
    }

    // CAST takes MySQL's own targets: integers as SIGNED or UNSIGNED, text as CHAR, binary as BINARY (with a length where
    // the column has one); DECIMAL, DOUBLE, FLOAT, DATE, DATETIME, TIME and JSON as they are.
    public override string CastTarget(string storeType)
    {
        var open = storeType.IndexOf('(', StringComparison.Ordinal);
        var name = (open < 0 ? storeType : storeType[..open]).Trim().ToUpperInvariant();
        var length = open < 0 ? "" : storeType[open..storeType.IndexOf(')', open)] + ")";
        var unsigned = storeType.Contains("UNSIGNED", StringComparison.OrdinalIgnoreCase);
        return name.Replace(" UNSIGNED", "", StringComparison.Ordinal) switch
        {
            "TINYINT" or "SMALLINT" or "MEDIUMINT" or "INT" or "INTEGER" or "BIGINT" or "BIT" or "BOOL" or "BOOLEAN" or "YEAR"
                => unsigned ? "UNSIGNED" : "SIGNED",
            "CHAR" or "VARCHAR" => "CHAR" + length,
            "TINYTEXT" or "TEXT" or "MEDIUMTEXT" or "LONGTEXT" or "ENUM" or "SET" => "CHAR",
            "BINARY" or "VARBINARY" => "BINARY" + length,
            "TINYBLOB" or "BLOB" or "MEDIUMBLOB" or "LONGBLOB" => "BINARY",
            _ => storeType
        };
    }

    // No time-zone-aware type.
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(DateTimeOffset) ? null : StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);
}

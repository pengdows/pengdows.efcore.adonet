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

    /// <summary>
    ///     SingleStore, which speaks MySQL but accepts <c>SAVEPOINT</c> and <c>ROLLBACK TO SAVEPOINT</c> and ignores them, so
    ///     work rolled back to a savepoint would be committed: this dialect has no savepoints. MySqlConnector reports them
    ///     there anyway, since its <c>SupportsSavepoints</c> describes the driver, not the server
    ///     (https://github.com/mysql-net/MySqlConnector/issues/1677).
    /// </summary>
    public static readonly MySqlDialect SingleStore = new(backslashEscapes: true, savepoints: false, name: "SingleStore");

    private MySqlDialect(bool backslashEscapes, bool savepoints = true, string? name = null)
    {
        Name = name ?? (backslashEscapes ? "MySQL" : "MySQL (NO_BACKSLASH_ESCAPES)");
        Sql = new MySqlSql(backslashEscapes, savepoints);
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
        MaxIdentifierLength = 64,
        // The driver enlists in a TransactionScope. Behaviour reference (not copied):
        // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlRelationalConnection.cs
        SupportsAmbientTransactions = true
    };

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, MySqlMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, MySqlHistoryRepository>();
}

internal sealed class MySqlSql(bool backslashEscapes = true, bool savepoints = true) : AnsiSqlDialect
{
    // Through a connection to no database. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlDatabaseCreator.cs
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Server(
        "Database",
        adminDatabase: null);

    public override SavepointSyntax? Savepoints
        => savepoints ? SavepointSyntax.Iso : null;

    // MySQL's json type and functions. JSON_UNQUOTE turns a JSON null into the text "null", so a string is read only when the
    // value isn't null; a bool compares against JSON true and false; other types use JSON_VALUE ... RETURNING (8.0.21+),
    // which reads a JSON null as NULL. A patch value given as JSON text is CAST to JSON.
    public override JsonSyntax Json { get; } = new(
        "json",
        "CASE JSON_TYPE(JSON_EXTRACT({0}, {1})) WHEN 'NULL' THEN NULL ELSE JSON_UNQUOTE(JSON_EXTRACT({0}, {1})) END",
        "JSON_EXTRACT({0}, {1})",
        "JSON_VALUE({0}, {1} RETURNING {2})",
        "CASE JSON_EXTRACT({0}, {1}) WHEN CAST('true' AS JSON) THEN TRUE WHEN CAST('false' AS JSON) THEN FALSE END",
        "CONCAT({0}, {1})",
        "JSON_SET({0}, {1}, {2})",
        "CAST({0} AS JSON)");

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
    // Bitwise operators return a 64-bit unsigned integer (~12 is 18446744073709551603), so each result is read back as signed.
    // Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionVisitors/Internal/BitwiseOperationReturnTypeCorrectingExpressionVisitor.cs
    public override string? BitwiseAnd => "CAST({0} & {1} AS SIGNED)";
    public override string? BitwiseOr => "CAST({0} | {1} AS SIGNED)";
    public override string? BitwiseNot => "CAST(~{0} AS SIGNED)";
    public override string? ExclusiveOr => "CAST({0} ^ {1} AS SIGNED)";

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

    // One datetime type: bound as DbType.DateTime, as both MySQL EF providers bind it. Oracle's MySql.Data maps
    // DbType.DateTime2 to a BLOB and sends current-culture text (UPSTREAM_ISSUES.md); MySqlConnector takes either.
    public override System.Data.DbType DateTimeDbType
        => System.Data.DbType.DateTime;

    // TEXT and BLOB can't be keys or indexed without a prefix length, and InnoDB's index key holds 3072 bytes: an unsized key or
    // index column is VARCHAR(255) (half the key at utf8mb4's 4 bytes a character, at most 255) or VARBINARY(3072).
    // Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlTypeMappingSource.cs
    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => facets switch
        {
            { KeyOrIndex: true, Size: null } when clrType == typeof(string) => "VARCHAR(255)",
            { KeyOrIndex: true, Size: null } when clrType == typeof(byte[]) => "VARBINARY(3072)",
            // Every VARCHAR and VARBINARY byte counts toward a 65,535-byte row: a string longer than fits at utf8mb4's 4 bytes
            // a character is LONGTEXT, and binary of 8,000 bytes or more LONGBLOB. Adapted from Pomelo's MySqlTypeMappingSource
            // and MySqlByteArrayTypeMapping (MIT, Pomelo Foundation).
            { Size: > MaxVarcharCharacters } when clrType == typeof(string) => "LONGTEXT",
            { Size: >= MinBlobBytes } when clrType == typeof(byte[]) => "LONGBLOB",
            _ => base.GetStoreType(clrType, facets)
        };

    private const int MaxVarcharCharacters = 65_535 / 4;
    private const int MinBlobBytes = 8000;

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

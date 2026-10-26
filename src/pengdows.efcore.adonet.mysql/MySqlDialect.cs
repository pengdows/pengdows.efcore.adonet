using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL. ISO quoted identifiers (under ANSI_QUOTES, set for each session), a database is the schema, OFFSET needs a LIMIT, and keys come back through
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

    public override pengdows.efcore.adonet.Scaffolding.InformationSchemaReader SchemaReader
        => MySqlSchemaReader.Instance;

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
        "CAST({0} AS JSON)")
    {
        // FOR ORDINALITY numbers the elements in array order.
        ArrayElements = "JSON_TABLE({0}, '$[*]' COLUMNS ({1} FOR ORDINALITY, {2} {3} PATH '$'))",

        // A JSON_TABLE text column takes the connection's collation as strongly as a table column takes its own, so comparing the
        // two fails when they differ ("Illegal mix of collations"); a _bin collation yields to neither and compares ordinally,
        // as .NET does.
        ArrayElementsTextType = "{0} CHARACTER SET utf8mb4 COLLATE utf8mb4_bin",
        ArrayElementsBinaryValue = "FROM_BASE64({0})",

        // An IN or EXISTS subquery turned into a semijoin, or a lateral derived table merged into its query, over a JSON_TABLE of
        // an outer column reads the column as empty ("The document is empty"); the semijoin case is fixed in 9.3.0
        // (https://bugs.mysql.com/bug.php?id=114897, UPSTREAM_ISSUES.md).
        ArrayElementsHint = "/*+ NO_SEMIJOIN() NO_MERGE() */"
    };

    // ISO quoted identifiers ("name"), which MySQL reads only under ANSI_QUOTES: each session adds it to its sql_mode. String
    // literals are always single-quoted, so nothing else changes meaning.
    public override string? SessionSetup
        => "SET SESSION sql_mode = CONCAT_WS(',', NULLIF(@@SESSION.sql_mode, ''), 'ANSI_QUOTES')";
    public override bool SupportsSchemas => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset(noLimitValue: "18446744073709551615").WithValueLimitsOnly();
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("LAST_INSERT_ID()", "ROW_COUNT()");
    public override DmlSyntax Dml => DmlSyntax.MultiTable;
    public override FunctionSyntax Functions { get; } = new MySqlFunctions();
    public override string DefaultValues => "() VALUES ()";
    public override string ValuesRowPrefix => "ROW";
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

    // Shifts work on 64-bit unsigned integers, so each result is read back as signed, as the bitwise operators are.
    public override string? LeftShift => "CAST({0} << {1} AS SIGNED)";
    public override string? RightShift => "CAST({0} >> {1} AS SIGNED)";

    // Behaviour reference for the math, string and date and time functions below (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/Internal/MySqlMathMethodTranslator.cs
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/Internal/MySqlDateTimeMemberTranslator.cs
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/Internal/MySqlDateTimeMethodTranslator.cs
    public override string? Atan2 => "ATAN2({0}, {1})";
    public override string? Log2 => "LOG2({0})";
    public override string? Sign => "SIGN({0})";
    public override string? Truncate => "TRUNCATE({0}, 0)";
    public override string? Degrees => "DEGREES({0})";
    public override string? Radians => "RADIANS({0})";
    public override string? Greatest => "GREATEST({0}, {1})";
    public override string? Least => "LEAST({0}, {1})";

    // MySQL has no hyperbolic functions: their definitions in EXP and LN.
    public override string? Cosh => "((EXP({0}) + EXP(-({0}))) / 2)";
    public override string? Sinh => "((EXP({0}) - EXP(-({0}))) / 2)";
    public override string? Tanh => "((EXP({0}) - EXP(-({0}))) / (EXP({0}) + EXP(-({0}))))";
    public override string? Acosh => "LN({0} + SQRT({0} * {0} - 1))";
    public override string? Asinh => "LN({0} + SQRT({0} * {0} + 1))";
    public override string? Atanh => "(LN((1 + {0}) / (1 - {0})) / 2)";

    public override string? PositionFrom => "LOCATE({0}, {1}, {2})";

    // 'c': case-sensitive whatever the collation, as .NET's Regex. An empty pattern matches everything in .NET and is an error
    // in MySQL, so it is matched before REGEXP_LIKE sees it.
    public override string? RegexIsMatch => "CASE WHEN {1} = '' THEN TRUE ELSE REGEXP_LIKE({0}, {1}, 'c') END";
    public override string? NewGuid => "UUID()";

    public override string? DayOfYear => "DAYOFYEAR({0})";
    public override string? DayOfWeek => "(DAYOFWEEK({0}) - 1)";
    public override string? Millisecond => "(EXTRACT(MICROSECOND FROM {0}) DIV 1000)";
    public override string? Microsecond => "(EXTRACT(MICROSECOND FROM {0}) % 1000)";

    // DATETIME(6) and TIME(6) keep microseconds.
    public override string? Nanosecond => "CASE WHEN {0} IS NOT NULL THEN 0 END";
    public override string? DateOf => "DATE({0})";
    public override string? TimeOfDay => "CAST({0} AS TIME(6))";
    public override string? Now => "CURRENT_TIMESTAMP(6)";
    public override string? UtcNow => "UTC_TIMESTAMP(6)";
    public override string? Today => "CURDATE()";
    public override string? AddYears => "DATE_ADD({0}, INTERVAL {1} YEAR)";
    public override string? AddMonths => "DATE_ADD({0}, INTERVAL {1} MONTH)";
    public override string? AddDays => "DATE_ADD({0}, INTERVAL {1} DAY)";
    public override string? AddMicroseconds => "DATE_ADD({0}, INTERVAL ROUND({1}) MICROSECOND)";

    // TO_DAYS counts from year 0, 366 days before 0001-01-01.
    public override string? DayNumber => "(TO_DAYS({0}) - 366)";
    public override string? DateFromDateTime => "DATE({0})";
    public override string? TimeFromDateTime => "CAST({0} AS TIME(6))";
    public override string? TimeFromTimeSpan => "{0}";
    public override string? DateAtTime => "TIMESTAMP({0}, {1})";
    public override string? AddTime => "ADDTIME({0}, {1})";
    public override string? TimeIsBetween => "CASE WHEN {1} <= {2} THEN {0} >= {1} AND {0} < {2} ELSE {0} >= {1} OR {0} < {2} END";

    // TIMEDIFF of two times may be negative; .NET's difference wraps past midnight.
    public override string? TimeDifference => "CASE WHEN {0} >= {1} THEN TIMEDIFF({0}, {1}) ELSE ADDTIME(TIMEDIFF({0}, {1}), '24:00:00') END";

    // A DateTimeOffset is stored as its UTC instant.
    public override string? OffsetNow => "UTC_TIMESTAMP(6)";
    public override string? UnixSeconds => "TIMESTAMPDIFF(SECOND, '1970-01-01', {0})";
    public override string? UnixMilliseconds => "(TIMESTAMPDIFF(MICROSECOND, '1970-01-01', {0}) DIV 1000)";

    public override string? ByteLength => "LENGTH({0})";
    public override string? ByteAt => "ASCII(SUBSTRING({0}, {1}, 1))";

    // CHAR(n) is the one-byte binary string of byte n.
    public override string? BytesContain => "(LOCATE(CHAR({1}), {0}) > 0)";

    // A TIME counts hours past a day; a TimeSpan's Hours are those of its last day.
    public override string? TimeSpanHours => "(EXTRACT(HOUR FROM {0}) % 24)";
    public override string? TimeSpanMinutes => "EXTRACT(MINUTE FROM {0})";
    public override string? TimeSpanSeconds => "EXTRACT(SECOND FROM {0})";
    public override string? TimeSpanMilliseconds => "(EXTRACT(MICROSECOND FROM {0}) DIV 1000)";
    public override string? TimeSpanMicroseconds => "(EXTRACT(MICROSECOND FROM {0}) % 1000)";
    public override string? TimeSpanNanoseconds => "CASE WHEN {0} IS NOT NULL THEN 0 END";
}

internal sealed class MySqlTypeMappings : AnsiTypeMappings
{
    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "TINYINT(1)",
        [typeof(byte)] = "TINYINT UNSIGNED",
        // MySQL's own name, as its catalog reports it.
        [typeof(int)] = "INT",
        [typeof(float)] = "FLOAT",
        [typeof(double)] = "DOUBLE",
        [typeof(string)] = "LONGTEXT",
        [typeof(DateTime)] = "DATETIME(6)",
        [typeof(TimeOnly)] = "TIME(6)",
        [typeof(TimeSpan)] = "TIME(6)",
        [typeof(Guid)] = "CHAR(36)",
        // No type keeps an offset: the instant, in UTC, read back with offset zero, as both MySQL EF providers store it.
        // The driver writes a DateTimeOffset parameter in UTC and reads a DATETIME as one at offset zero.
        [typeof(DateTimeOffset)] = "DATETIME(6)",
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
            // A size of -1 is EF's "as large as the type allows".
            { Size: > MaxVarcharCharacters or < 0 } when clrType == typeof(string) => "LONGTEXT",
            { Size: >= MinBlobBytes or < 0 } when clrType == typeof(byte[]) => "LONGBLOB",
            _ => base.GetStoreType(clrType, facets)
        };

    private const int MaxVarcharCharacters = 65_535 / 4;
    private const int MinBlobBytes = 8000;

    // TIME spans -838:59:59 to 838:59:59 and counts hours past a day; outside that MySQL would clip the value.
    public override string FormatLiteral(object value, string storeType, string literal)
    {
        // DATETIME keeps microseconds, and a literal (a column default) with more digits is refused, not rounded.
        if (value is DateTimeOffset offset)
        {
            return "TIMESTAMP '" + offset.UtcDateTime.ToString(@"yyyy\-MM\-dd HH\:mm\:ss.ffffff", CultureInfo.InvariantCulture) + "'";
        }

        if (value is DateTime dateTime)
        {
            return "TIMESTAMP '" + dateTime.ToString(@"yyyy\-MM\-dd HH\:mm\:ss.ffffff", CultureInfo.InvariantCulture) + "'";
        }

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

    // CAST(x AS CHAR) has the connection's collation, and as strongly as a column's (IMPLICIT), so comparing it with a column of
    // another collation fails ("Illegal mix of collations"). CONCAT(x) converts to text as weakly as a number does, so the
    // other side's collation wins, as it would for x itself. A sized CHAR(n) keeps CAST, which truncates to n.
    public override string Cast(string storeType)
        => CastTarget(storeType) == "CHAR" ? "CONCAT({0})" : base.Cast(storeType);

    public override string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);
}

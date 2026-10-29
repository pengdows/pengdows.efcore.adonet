using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.sqlite;

public class SqliteDialect : AdoNetDialect
{
    public static readonly SqliteDialect Instance = new();

    protected SqliteDialect()
    {
    }

    // No INFORMATION_SCHEMA: the catalog is sqlite_master and the PRAGMA table functions.
    public override SchemaReader SchemaReader
        => SqliteSchemaReader.Instance;

    public override string Name => "SQLite";
    public override ISqlDialect Sql { get; } = new SqliteSql();
    public override ITypeMappingDialect TypeMappings { get; } = new SqliteTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
        MaxParametersPerCommand = 32766,
        // SQLite has no limit; names are left whole, as Microsoft's SQLite provider leaves them.
        MaxIdentifierLength = int.MaxValue,
        // Microsoft.Data.Sqlite steps several statements on one connection, so split queries stream.
        SupportsMultipleActiveReaders = true
    };

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, SqliteMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, SqliteHistoryRepository>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IMethodCallTranslatorPlugin, SqliteMethodCallTranslatorPlugin>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IMemberTranslatorPlugin, SqliteMemberTranslatorPlugin>()
            .TryAdd<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor, SqliteConnectionFunctionsInterceptor>()
            .TryAdd<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor, SqliteSpatialiteInterceptor>()
            .TryAdd<Microsoft.EntityFrameworkCore.Metadata.IRelationalAnnotationProvider, SqliteAnnotationProvider>();
}

/// <summary>SQLite before 3.35 has no RETURNING; keys come back through <c>last_insert_rowid()</c>.</summary>
public sealed class LegacySqliteDialect : SqliteDialect
{
    public static new readonly LegacySqliteDialect Instance = new();

    private LegacySqliteDialect()
    {
    }

    public override string Name => "SQLite (pre-3.35)";
    public override ISqlDialect Sql { get; } = new LegacySqliteSql();
}

internal class SqliteSql : AnsiSqlDialect
{
    public override string ReturningClauseAnnotation => SqliteModelOptionsExtensions.UseSqlReturningClauseAnnotation;

    public override bool SupportsNullOrderingClauses => true;

    // A file, created in write-ahead-log mode, with its -wal and -shm files; :memory: or Mode=Memory exists while open.
    // Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Storage/Internal/SqliteDatabaseCreator.cs
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.File(
        "Data Source",
        ["-wal", "-shm"],
        "PRAGMA journal_mode = 'wal';",
        isInMemory: b => string.Equals(Convert.ToString(b.TryGetValue("Data Source", out var path) ? path : null, CultureInfo.InvariantCulture), ":memory:", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Convert.ToString(b.TryGetValue("Mode", out var mode) ? mode : null, CultureInfo.InvariantCulture), "Memory", StringComparison.OrdinalIgnoreCase));

    public override bool SupportsSchemas => false;
    public override bool SupportsStoredProcedures => false;

    // Only a key of one INTEGER column is the rowid, which the database assigns (efcore#26708).
    public override bool GeneratesCompositeKeyValues => false;

    // SQLite has no lateral join; a query needing one fails to translate, as with the official provider. Behaviour reference
    // (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqliteQueryTranslationPostprocessor.cs
    public override LateralJoinSyntax? LateralJoins => null;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset(noLimitValue: "-1");

    // A table with triggers, or a virtual table, refuses RETURNING (UseSqlReturningClause(false)). Behaviour reference (not
    // copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Update/Internal/SqliteUpdateSqlGenerator.cs
    public override SelectAfterKeyRetrieval? KeysWithoutReturning { get; } = KeyRetrieval.SelectAfter("last_insert_rowid()", "changes()");
    public override FunctionSyntax Functions { get; } = new SqliteFunctions();
    public override SpatialSyntax Spatial => SqliteSpatial.Syntax;
    public override JsonSyntax Json { get; } = SqliteJson.Syntax;

    public override string HasTablesSql
        => "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"rootpage\" IS NOT NULL";
}

internal sealed class LegacySqliteSql : SqliteSql
{
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("last_insert_rowid()", "changes()");
}

internal sealed class SqliteTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(string) ? "TEXT"
            : clrType == typeof(decimal) ? "TEXT"
            : clrType == typeof(byte[]) || clrType == typeof(Guid) ? "BLOB"

            // Every integer is INTEGER, which also makes any integer key the rowid.
            : clrType == typeof(long) || clrType == typeof(short) || clrType == typeof(byte) ? "INTEGER"
            : base.GetStoreType(clrType);

    // SQLite has no date, time or uuid types: Microsoft.Data.Sqlite binds them as text. A constant must be the same text, or
    // it won't compare equal to what a parameter stored.
    public override string FormatLiteral(object value, string storeType, string literal)
        => value switch
        {
            DateTime dateTime => Text(dateTime, @"yyyy\-MM\-dd HH\:mm\:ss.FFFFFFF"),
            DateTimeOffset offset => Text(offset, @"yyyy\-MM\-dd HH\:mm\:ss.FFFFFFFzzz"),
            DateOnly date => Text(date, @"yyyy\-MM\-dd"),
            TimeOnly time => Text(time, (time.Ticks % TimeSpan.TicksPerSecond) == 0 ? @"HH\:mm\:ss" : @"HH\:mm\:ss\.fffffff"),
            TimeSpan span => Text(span, "c"),
            Guid guid => "'" + guid.ToString().ToUpperInvariant() + "'",
            _ => literal
        };

    // SQLite has no exact decimal; a cast to DECIMAL or NUMERIC keeps integers integers (10250 / 11250 is 0). Fractional
    // numbers are REAL in SQLite, so a decimal cast computes as one.
    public override string CastTarget(string storeType)
        => storeType.StartsWith("DECIMAL", StringComparison.OrdinalIgnoreCase) || storeType.StartsWith("NUMERIC", StringComparison.OrdinalIgnoreCase)
            ? "REAL"
            : base.CastTarget(storeType);

    private static string Text(IFormattable value, string format)
        => "'" + value.ToString(format, CultureInfo.InvariantCulture) + "'";
}

/// <summary>
///     Dates are text, so their parts come from STRFTIME. STRFTIME moves a time with an offset to UTC, so the offset (always
///     six characters: +hh:mm) is cut off first, leaving the parts of the value's own time.
/// </summary>
internal sealed class SqliteFunctions : FunctionSyntax
{
    public override string? BitwiseAnd => "{0} & {1}";
    public override string? BitwiseOr => "{0} | {1}";
    public override string? BitwiseNot => "~{0}";

    // No exclusive or operator: (a | b) - (a & b), which equals it for integers. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqliteSqlTranslatingExpressionVisitor.cs
    public override string? ExclusiveOr => "(({0} | {1}) - ({0} & {1}))";

    // The % operator, as the database's EF Core provider writes it.
    public override string? Modulo => null;

    public override string? Trim => "TRIM({0})";
    public override string? Upper => "UPPER({0})";
    public override string? Lower => "LOWER({0})";
    public override string? Replace => "REPLACE({0}, {1}, {2})";
    public override string? Round => "ROUND({0})";
    public override string? RoundDigits => "ROUND({0}, {1})";
    public override string? Power => "POW({0}, {1})";
    public override string? Sqrt => "SQRT({0})";
    public override string? Acos => "ACOS({0})";
    public override string? Asin => "ASIN({0})";
    public override string? Atan => "ATAN({0})";
    public override string? Atan2 => "ATAN2({0}, {1})";
    public override string? Acosh => "ACOSH({0})";
    public override string? Asinh => "ASINH({0})";
    public override string? Atanh => "ATANH({0})";
    public override string? Cos => "COS({0})";
    public override string? Cosh => "COSH({0})";
    public override string? Sin => "SIN({0})";
    public override string? Sinh => "SINH({0})";
    public override string? Tan => "TAN({0})";
    public override string? Tanh => "TANH({0})";
    public override string? Exp => "EXP({0})";
    public override string? Ln => "LN({0})";
    public override string? Log10 => "LOG10({0})";
    public override string? LogBase => "LOG({1}, {0})";
    public override string? Log2 => "LOG2({0})";
    public override string? Sign => "SIGN({0})";
    public override string? Truncate => "TRUNC({0})";
    public override string? Degrees => "DEGREES({0})";
    public override string? Radians => "RADIANS({0})";
    public override string? Greatest => "MAX({0}, {1})";
    public override string? Least => "MIN({0}, {1})";
    public override string? DayOfYear => "CAST(STRFTIME('%j', {0}) AS INTEGER)";
    public override string? DayOfWeek => "CAST(STRFTIME('%w', {0}) AS INTEGER)";
    public override string? Millisecond => "CAST((CAST(STRFTIME('%f', {0}) AS REAL) * 1000) AS INTEGER) % 1000";
    public override string? DateOf => "STRFTIME('%Y-%m-%d 00:00:00', {0})";
    // A computed date and time drops its fraction's trailing zeros, and a bare point, so it is the text Microsoft.Data.Sqlite
    // writes for the same value and compares equal to it. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/Translators/SqliteDateTimeMethodTranslator.cs
    public override string? Now => "RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', 'now', 'localtime'), '0'), '.')";
    public override string? UtcNow => "RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')";
    public override string? Today => "STRFTIME('%Y-%m-%d 00:00:00', 'now', 'localtime')";
    public override string? AddYears => "RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', {0}, CAST({1} AS TEXT) || ' years'), '0'), '.')";
    public override string? AddMonths => "RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', {0}, CAST({1} AS TEXT) || ' months'), '0'), '.')";
    public override string? AddDays => "RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', {0}, CAST({1} AS TEXT) || ' days'), '0'), '.')";

    // Hours, minutes, seconds and milliseconds come as microseconds; STRFTIME keeps milliseconds.
    public override string? AddMicroseconds
        => "RTRIM(RTRIM(STRFTIME('%Y-%m-%d %H:%M:%f', {0}, CAST(({1}) / 1000000.0 AS TEXT) || ' seconds'), '0'), '.')";

    // JULIANDAY preserves the fractional-day result that EF exposes through
    // TimeSpan.TotalDays; unlike SQLite's date subtraction, it accepts the
    // ISO/EF timestamp text emitted by this provider.
    public override string? DateDifferenceTotalDays => "(JULIANDAY({0}) - JULIANDAY({1}))";

    public override string? TimeOfDay => "RTRIM(RTRIM(STRFTIME('%H:%M:%f', {0}), '0'), '.')";

    // ISO's CAST(x AS DATE) is a NUMERIC cast in SQLite: the year alone.
    public override string? DateFromDateTime => "DATE({0})";
    public override string? DayNumber => "CAST(JULIANDAY({0}) - JULIANDAY('0001-01-01') AS INTEGER)";

    // INSTR from a position: the position in the rest of the string, made absolute; 0 when absent.
    public override string? PositionFrom
        => "CASE WHEN INSTR(SUBSTR({1}, {2}), {0}) > 0 THEN INSTR(SUBSTR({1}, {2}), {0}) + {2} - 1 ELSE 0 END";

    // Regex.IsMatch: REGEXP calls the regexp function SqliteConnectionFunctionsInterceptor gives each connection.
    public override string? RegexIsMatch => "({0} REGEXP {1})";

    // A blob's LENGTH is its bytes. SQLite has no function giving a byte as a number, so one is read from its two hex digits.
    public override string? ByteLength => "LENGTH({0})";

    public override string? ByteAt
        => "((INSTR('0123456789ABCDEF', SUBSTR(HEX(SUBSTR({0}, {1}, 1)), 1, 1)) - 1) * 16 "
            + "+ INSTR('0123456789ABCDEF', SUBSTR(HEX(SUBSTR({0}, {1}, 1)), 2, 1)) - 1)";

    // The byte as a one-byte blob (CHAR would make a byte above 127 two bytes of UTF-8).
    public override string? BytesContain => "(INSTR({0}, UNHEX(PRINTF('%02X', {1}))) > 0)";

    // NUMERIC affinity stores a whole decimal as an INTEGER, and SQLite divides two INTEGERs as integers; its % truncates
    // both operands to integers, where MOD keeps the fraction (SQLite 3.35's math functions).
    public override string? DecimalDivide => "CAST({0} AS REAL) / {1}";
    public override string? FractionalModulo => "MOD({0}, {1})";

    public override string? OffsetDateTime => "SUBSTR({0}, 1, LENGTH({0}) - 6)";
    public override string? TrimStart => "LTRIM({0})";
    public override string? TrimEnd => "RTRIM({0})";

    // trim(X, Y) removes any of Y's characters from X's ends; SQLite has no TRIM(BOTH ... FROM ...). Behaviour reference
    // (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/Translators/SqliteStringMethodTranslator.cs
    public override string? TrimCharacter => "TRIM({0}, {1})";
    public override string? TrimStartCharacter => "LTRIM({0}, {1})";
    public override string? TrimEndCharacter => "RTRIM({0}, {1})";
    public override string? TrimCharacters => "TRIM({0}, {1})";
    public override string? TrimStartCharacters => "LTRIM({0}, {1})";
    public override string? TrimEndCharacters => "RTRIM({0}, {1})";

    // No LPAD/RPAD: a run of n pad characters (hex(zeroblob(n)) is n pairs of zeros) joined to the string, then cut to n.
    public override string? PadLeft
        => "CASE WHEN LENGTH({0}) >= {1} THEN {0} ELSE SUBSTR(REPLACE(HEX(ZEROBLOB({1})), '00', {2}) || {0}, -({1})) END";

    public override string? PadRight
        => "CASE WHEN LENGTH({0}) >= {1} THEN {0} ELSE SUBSTR({0} || REPLACE(HEX(ZEROBLOB({1})), '00', {2}), 1, {1}) END";
    public override string? Length => "LENGTH({0})";
    public override string? Substring => "SUBSTR({0}, {1}, {2})";
    public override string? SubstringFrom => "SUBSTR({0}, {1})";
    public override string? Position => "INSTR({1}, {0})";
    public override string? Year => "CAST(STRFTIME('%Y', {0}) AS INTEGER)";
    public override string? Month => "CAST(STRFTIME('%m', {0}) AS INTEGER)";
    public override string? Day => "CAST(STRFTIME('%d', {0}) AS INTEGER)";
    public override string? Hour => "CAST(STRFTIME('%H', {0}) AS INTEGER)";
    public override string? Minute => "CAST(STRFTIME('%M', {0}) AS INTEGER)";
    public override string? Second => "CAST(STRFTIME('%S', {0}) AS INTEGER)";
}

internal static class SqliteJson
{
    public static JsonSyntax Syntax { get; } = new(
        "TEXT",
        "json_extract({0}, {1})",
        "json_extract({0}, {1})",
        "CAST(json_extract({0}, {1}) AS {2})",
        "CASE json_extract({0}, {1}) WHEN 1 THEN TRUE WHEN 0 THEN FALSE END",
        "CAST(json_extract({0}, {1}) AS BLOB)",
        "({0} || {1})",
        "json_set({0}, {1}, {2})",
        "json({0})")
    {
        StoresDocumentAsText = true,
        ArrayElements = "(SELECT CAST(key AS INTEGER) AS {1}, CAST(value AS {3}) AS {2} FROM json_each({0}))",
        ArrayElementsTextType = "TEXT"
    };
}

using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.sqlite;

public class SqliteDialect : AdoNetDialect
{
    public static readonly SqliteDialect Instance = new();

    protected SqliteDialect()
    {
    }

    public override string Name => "SQLite";
    public override ISqlDialect Sql { get; } = new SqliteSql();
    public override ITypeMappingDialect TypeMappings { get; } = new SqliteTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
        MaxParametersPerCommand = 32766,
        // SQLite has no limit; names are left whole, as Microsoft's SQLite provider leaves them.
        MaxIdentifierLength = int.MaxValue
    };

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, SqliteMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, SqliteHistoryRepository>();
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
    // A file, created in write-ahead-log mode, with its -wal and -shm files; :memory: or Mode=Memory exists while open.
    // Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Storage/Internal/SqliteDatabaseCreator.cs
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.File(
        "Data Source",
        ["-wal", "-shm"],
        "PRAGMA journal_mode = 'wal';",
        isInMemory: b => string.Equals(Convert.ToString(b.TryGetValue("Data Source", out var path) ? path : null, CultureInfo.InvariantCulture), ":memory:", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Convert.ToString(b.TryGetValue("Mode", out var mode) ? mode : null, CultureInfo.InvariantCulture), "Memory", StringComparison.OrdinalIgnoreCase),
        clearPool: c => Microsoft.Data.Sqlite.SqliteConnection.ClearPool((Microsoft.Data.Sqlite.SqliteConnection)c));

    public override bool SupportsSchemas => false;

    // SQLite has no lateral join; a query needing one fails to translate, as with the official provider. Behaviour reference
    // (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Query/Internal/SqliteQueryTranslationPostprocessor.cs
    public override LateralJoinSyntax? LateralJoins => null;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset(noLimitValue: "-1");
    public override FunctionSyntax Functions { get; } = new SqliteFunctions();

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

    public override string? OffsetDateTime => "SUBSTR({0}, 1, LENGTH({0}) - 6)";
    public override string? TrimStart => "LTRIM({0})";
    public override string? TrimEnd => "RTRIM({0})";
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

using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.duckdb;

public sealed class DuckDbDialect : AdoNetDialect
{
    public static readonly DuckDbDialect Instance = new();

    private DuckDbDialect()
    {
    }

    public override string Name => "DuckDB";
    public override ISqlDialect Sql { get; } = new DuckDbSql();
    public override ITypeMappingDialect TypeMappings { get; } = new DuckDbTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
        MaxParametersPerCommand = 65535,
        // DuckDB.NET 1.5.6.1 returns a BLOB only as a stream; GetFieldValue<byte[]> throws InvalidCastException. Fixed
        // upstream, unreleased: https://github.com/Giorgi/DuckDB.NET/pull/384
        ReadsBinaryAsStream = true
    };

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, DuckDbMigrationsSqlGenerator>();
}

internal sealed class DuckDbSql : AnsiSqlDialect
{
    public override bool SupportsStoredProcedures => false;
    public override bool SupportsNullOrderingClauses => true;

    // A file with its .wal; :memory: (or no path) exists while open.
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.File(
        "Data Source",
        [".wal"],
        isInMemory: b => (Convert.ToString(b.TryGetValue("Data Source", out var path) ? path : null, CultureInfo.InvariantCulture) is not { Length: > 0 } text)
            || text.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase));

    public override string ParameterMarker => "$";
    public override bool ParameterNameIncludesMarker => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset();
    public override FunctionSyntax Functions { get; } = new DuckDbFunctions();
    public override string CurrentSchema => "current_schema()";

    // DuckDB has no savepoints.
    public override SavepointSyntax? Savepoints => null;
}

/// <summary>
///     A TIMESTAMPTZ holds an instant, whose parts EXTRACT takes in the session's time zone; DuckDB.NET reads it back at UTC,
///     so its parts are taken at UTC.
/// </summary>
internal sealed class DuckDbFunctions : FunctionSyntax
{
    // CURRENT_TIMESTAMP is an instant shown in the session's time zone; AT TIME ZONE 'UTC' gives its UTC timestamp.
    public override string? UtcNow => "(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')";

    // trim/ltrim/rtrim(string, characters) remove any of the characters.
    public override string? TrimCharacters => "TRIM({0}, {1})";
    public override string? PadLeft => "CASE WHEN LENGTH({0}) >= {1} THEN {0} ELSE LPAD({0}, {1}, {2}) END";
    public override string? PadRight => "CASE WHEN LENGTH({0}) >= {1} THEN {0} ELSE RPAD({0}, {1}, {2}) END";
    public override string? TrimStartCharacters => "LTRIM({0}, {1})";
    public override string? TrimEndCharacters => "RTRIM({0}, {1})";

    // ^ is exponentiation in DuckDB; xor() is exclusive or. Behaviour reference (not copied):
    // https://github.com/skuirrels/DuckDB.EFCoreProvider/blob/f49a8c3e25f28055ad139383d25e09cede39c6f8/src/DuckDB.EFCoreProvider/Query/Internal/DuckDBSqlTranslatingExpressionVisitor.cs
    public override string? BitwiseAnd => "{0} & {1}";
    public override string? BitwiseOr => "{0} | {1}";
    public override string? BitwiseNot => "~{0}";
    public override string? ExclusiveOr => "xor({0}, {1})";

    // The % operator, as the database's EF Core provider writes it.
    public override string? Modulo => null;

    public override string? OffsetDateTime => "({0} AT TIME ZONE 'UTC')";
}

internal sealed class DuckDbTypeMappings : AnsiTypeMappings
{
    // DuckDB.NET 1.5.6.1 converts a parameter whose type the statement doesn't fix by its DbType. It has no conversion for
    // Decimal or DateTime2, sending such a value as the current culture's text; Currency and DateTime are its own for decimal
    // and DateTime (https://github.com/Giorgi/DuckDB.NET/pull/382). A Time-typed TimeSpan is made a time of day, refusing a
    // negative span or one of a day or more; any other TimeSpan is an INTERVAL, which is its store type here.
    public override System.Data.DbType ParameterDbType(Type clrType, System.Data.DbType standard)
        => clrType == typeof(decimal) ? System.Data.DbType.Currency
            : clrType == typeof(TimeSpan) ? System.Data.DbType.Object
            : standard;

    public override System.Data.DbType DateTimeDbType
        => System.Data.DbType.DateTime;

    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BLOB" : base.GetStoreType(clrType);

    // A blob constant is a string of \xHH escapes cast to BLOB; TIMESTAMP '...' would drop an offset.
    public override string FormatLiteral(object value, string storeType, string literal)
        => value switch
        {
            byte[] bytes => "'" + string.Concat(bytes.Select(b => @"\x" + b.ToString("X2", CultureInfo.InvariantCulture))) + "'::BLOB",
            DateTimeOffset offset => "TIMESTAMPTZ '" + offset.ToString(@"yyyy\-MM\-dd HH\:mm\:ss.FFFFFFzzz", CultureInfo.InvariantCulture) + "'",
            _ => literal
        };

    // BLOB takes no length.
    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => clrType == typeof(byte[]) ? null : base.GetStoreType(clrType, facets);
}

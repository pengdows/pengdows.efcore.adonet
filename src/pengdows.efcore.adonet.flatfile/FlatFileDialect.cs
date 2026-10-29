namespace pengdows.efcore.adonet.flatfile;

/// <summary>
///     pengdows.flatfile: strict ISO SQL. Named parameters are <c>:name</c>; there is no RETURNING or last-insert function,
///     so keys come from <c>NEXT VALUE FOR</c> sequences.
/// </summary>
public sealed class FlatFileDialect : AdoNetDialect
{
    public static readonly FlatFileDialect Instance = new();

    private FlatFileDialect()
    {
    }

    public override string Name => "pengdows.flatfile";
    public override ITypeMappingDialect TypeMappings { get; } = new FlatFileTypeMappings();
    public override ISqlDialect Sql { get; } = new FlatFileSql();
    // The engine implements the ISO SQL/JSON value/query functions and JSON_TABLE. JSON_SET is
    // deliberately absent: it is not ISO SQL, so EF JSON partial updates remain an explicit
    // capability boundary instead of silently emitting vendor syntax.
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
        MaxParametersPerCommand = 999,
        // Index sidecars include both the table and index names. Keep the generated component below common filesystem
        // limits even when EF combines two long model identifiers.
        MaxIdentifierLength = 63,
        // Readers are independent file streams.  A split query can therefore keep its result readers active on the same
        // connection, just as the flat-file engine permits multiple read-only connections alongside its single writer.
        SupportsMultipleActiveReaders = true
    };

}

// The flatfile engine represents an ISO INTERVAL literal as its internal IntervalValue
// expression.  That value cannot currently be converted back to TimeSpan when it is used
// by EF's migration seed INSERTs.  Keep the literal's invariant TimeSpan text for those
// inserts; parameterized queries still use the native INTERVAL path.
internal sealed class FlatFileTypeMappings : AnsiTypeMappings
{
    public override System.Data.DbType ParameterDbType(Type clrType, System.Data.DbType standard)
        // DbType.Time makes the engine expose a TimeSpan parameter as TimeOnly.
        // INTERVAL arithmetic needs the CLR duration so the engine can apply
        // its temporal scaling and midnight-wrapping rules.
        => clrType == typeof(TimeSpan) ? System.Data.DbType.Object : base.ParameterDbType(clrType, standard);

    public override string FormatLiteral(object value, string storeType, string literal)
        => value is TimeSpan && storeType.StartsWith("INTERVAL", StringComparison.OrdinalIgnoreCase)
            ? literal
            : base.FormatLiteral(value, storeType, literal);
}

internal sealed class FlatFileSql : AnsiSqlDialect
{
    public override bool SupportsStoredProcedures => false;
    public override bool SupportsNullOrderingClauses => true;
    public override FunctionSyntax Functions { get; } = new FlatFileFunctions();

    // A database is a directory of table files.
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Directory("path");

    public override string ParameterMarker => ":";
    public override bool ParameterNameIncludesMarker => false;
    // Pengdows SQL data-change delta tables: FINAL/NEW/OLD TABLE reads generated row images back.
    // The Pengdows SQL specification's INCLUDE extension is the correlation path for batched source rows.
    public override KeyRetrieval Keys { get; } = KeyRetrieval.DeltaTableWithInclude;

    // CURRENT_SCHEMA is NOT advertised yet: the engine's CURRENT_SCHEMA still resolves to the only/most-recently
    // declared catalog schema rather than a root schema distinct from it (verified against 0.2.1-preview.20).
    // Once a tenant schema is created, CURRENT_SCHEMA stops meaning "no schema" and starts meaning "that schema",
    // so an unqualified lookup collides with a schema-qualified one of the same table name. See flatfile-gaps.md.

    // ISO SQL:2003 has no UPDATE ... FROM: an update through a join is written with correlated subqueries.
    public override DmlSyntax Dml => DmlSyntax.Iso;

    public override JsonSyntax? Json { get; } = new JsonSyntax(
        "JSON",
        "JSON_VALUE({0}, {1})",
        "JSON_QUERY({0}, {1})",
        "JSON_VALUE({0}, {1} RETURNING {2})",
        booleanValue: null,
        binaryValue: null,
        "{0} || {1}",
        set: null,
        "JSON({0})")
    {
        ArrayElements = "JSON_TABLE({0}, '$[*]' COLUMNS ({1} FOR ORDINALITY, {2} {3} PATH '$'))"
    };
}

internal sealed class FlatFileFunctions : FunctionSyntax
{
    public override string? DateOf => "CAST({0} AS DATE)";
    public override string? TimeOfDay => "CAST({0} AS TIME)";
    public override string? Now => "CURRENT_TIMESTAMP";
    public override string? Today => "CURRENT_DATE";

    public override string? AddYears => "({0} + ({1} * INTERVAL '1' YEAR))";
    public override string? AddMonths => "({0} + ({1} * INTERVAL '1' MONTH))";
    public override string? AddDays => "({0} + ({1} * INTERVAL '1' DAY))";
    public override string? AddMicroseconds => "({0} + (({1} / 1000000.0) * INTERVAL '1' SECOND))";
    public override string? TimeSpanAdd => "({0} + INTERVAL {1} HOUR TO SECOND)";
    public override string? TimeSpanSubtract => "({0} - INTERVAL {1} HOUR TO SECOND)";
    public override string? TimeSpanHours => "EXTRACT(HOUR FROM {0})";
    public override string? TimeSpanMinutes => "EXTRACT(MINUTE FROM {0})";

}

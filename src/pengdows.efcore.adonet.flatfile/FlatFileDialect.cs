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

internal sealed class FlatFileSql : AnsiSqlDialect
{
    public override bool SupportsNullOrderingClauses => true;

    // A database is a directory of table files.
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Directory("path");

    public override string ParameterMarker => ":";
    public override bool ParameterNameIncludesMarker => false;
    public override KeyRetrieval Keys { get; } = KeyRetrieval.Sequence();

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

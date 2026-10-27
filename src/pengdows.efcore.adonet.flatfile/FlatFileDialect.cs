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
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
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
    // A database is a directory of table files.
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Directory("path");

    public override string ParameterMarker => ":";
    public override bool ParameterNameIncludesMarker => false;
    public override KeyRetrieval Keys { get; } = KeyRetrieval.Sequence();
}

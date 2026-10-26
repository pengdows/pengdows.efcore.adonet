namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     Databases that take one writer at a time (SQLite, DuckDB, pengdows.flatfile): each one's test classes form one xUnit
///     collection, so they run one after another, while different databases still run in parallel. A test-run arrangement:
///     EF can't give such a database a second writer.
/// </summary>
public static class SingleWriter
{
    public const string Sqlite = "SQLite: one writer";
    public const string DuckDb = "DuckDB: one writer";
    public const string FlatFile = "pengdows.flatfile: one writer";
}

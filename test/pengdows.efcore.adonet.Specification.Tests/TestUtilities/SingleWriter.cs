namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     Database suites whose shared stores must not be reset concurrently: each one's test classes form one xUnit
///     collection, so they run one after another, while different databases still run in parallel.
/// </summary>
public static class SingleWriter
{
    public const string Sqlite = "SQLite: one writer";
    public const string DuckDb = "DuckDB: one writer";
    public const string FlatFile = "pengdows.flatfile: one writer";
    public const string MySql = "MySQL: shared stores";
}

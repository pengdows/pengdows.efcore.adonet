using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.duckdb;
using pengdows.efcore.adonet.flatfile;
using pengdows.efcore.adonet.mysql;
using pengdows.efcore.adonet.postgresql;
using pengdows.efcore.adonet.sqlite;
using pengdows.efcore.adonet.sqlserver;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     The capability matrix for the six providers, as an executable record: what each database engine, driver or adapter
///     does differently for the settings the shared core reads from the dialect, and why. A change to a dialect that
///     moves a cell fails here until the matrix, and the reason beside it, is updated on purpose.
/// </summary>
/// <remarks>
///     Null ordering: EF's tests, .NET and SQL Server sort nulls first. PostgreSQL, DuckDB and FlatFile's ISO engine sort them
///     last by default but take NULLS FIRST / NULLS LAST; SQLite sorts them first and takes the clauses; MySQL and SQL Server
///     sort them first and take no clauses.
///     DateTimeOffset: Npgsql writes timestamptz from offset-zero values only; every other driver takes any offset.
///     JSON document: SQL Server stores it as plain nvarchar text; PostgreSQL's jsonb is a binary document.
/// </remarks>
public sealed class DialectCapabilityMatrixTests
{
    public static IEnumerable<object[]> Matrix()
    {
        // name, dialect, takes NULLS FIRST/LAST, needs DateTimeOffset at offset zero, JSON document is plain text, JSON is a binary document
        yield return ["SQL Server", SqlServerDialect.Instance, false, false, true, false];
        yield return ["PostgreSQL", PostgreSqlDialect.Instance, true, true, false, true];
        yield return ["MySQL", MySqlDialect.Instance, false, false, false, false];
        yield return ["SQLite", SqliteDialect.Instance, true, false, false, false];
        yield return ["DuckDB", DuckDbDialect.Instance, true, false, false, false];
        yield return ["FlatFile", FlatFileDialect.Instance, true, false, false, false];
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Each_provider_declares_the_same_capabilities_the_matrix_records(
        string name, IAdoNetDialect dialect, bool nullClauses, bool utcOnly, bool jsonAsText, bool jsonBinary)
    {
        Assert.True(nullClauses == dialect.Sql.SupportsNullOrderingClauses, $"{name}: NULLS FIRST/LAST");
        Assert.True(utcOnly == dialect.TypeMappings.WritesDateTimeOffsetAsUtc, $"{name}: DateTimeOffset at offset zero");
        Assert.True(jsonAsText == (dialect.Sql.Json?.StoresDocumentAsText ?? false), $"{name}: JSON document as text");
        Assert.True(jsonBinary == (dialect.Sql.Json?.UsesBinaryDocument ?? false), $"{name}: JSON binary document");
    }
}

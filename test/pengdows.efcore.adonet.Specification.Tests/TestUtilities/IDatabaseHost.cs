using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>Everything database-specific the base suite needs: where an empty database comes from, and how a context uses it.</summary>
public interface IDatabaseHost
{
    /// <summary>A connection string for the named database, which <see cref="CreateEmptyDatabaseAsync" /> made.</summary>
    string ConnectionStringFor(string database);

    /// <summary>Drops the named database if it exists and creates it empty.</summary>
    Task CreateEmptyDatabaseAsync(string database);

    DbConnection CreateConnection(string connectionString);

    DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, string connectionString, Action<AdoNetDbContextOptionsBuilder> configure);

    /// <summary>Configures a context on an existing connection, which the context doesn't own.</summary>
    DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder builder, DbConnection connection, Action<AdoNetDbContextOptionsBuilder> configure);

    IAdoNetDialect Dialect { get; }

    /// <summary>The finest step the database's default date and time types keep: a 1-tick .NET value comes back rounded to it.</summary>
    TimeSpan TemporalResolution { get; }

    /// <summary>Whether a <see cref="DateTimeOffset" /> reads back with its offset; false where the database keeps only the instant.</summary>
    bool KeepsOffsets { get; }

    /// <summary>A valid baseline DateTime for provider-neutral built-in type probes.</summary>
    DateTime DefaultDateTime => new();

    /// <summary>Whether floating-point equality probes can require exact database equality.</summary>
    bool StrictEquality => true;

    /// <summary>Northwind's stored procedures, for EF's raw-SQL suites; null where the database has none.</summary>
    NorthwindProcedures? Procedures { get; }

    /// <summary>A collation other than the test databases' own, for the migrations suites; null where there is none to name.</summary>
    string? NonDefaultCollation { get; }

    /// <summary>Collation used by the shared Northwind string-comparison tests when an insensitive comparison is required.</summary>
    string? CaseInsensitiveCollation => null;

    /// <summary>Collation used by the shared Northwind string-comparison tests when a sensitive comparison is required.</summary>
    string? CaseSensitiveCollation => null;

    /// <summary>Database schema used by mapping tests; null for engines without schemas.</summary>
    string? DatabaseSchema => null;

    /// <summary>EF's stored-procedure update suite on this database; null where the database has no procedures.</summary>
    UpdateProcedures? UpdateProcedures { get; }

    /// <summary>Whether the shared store keeps its connection open after initialization.</summary>
    bool KeepStoreConnectionOpen { get; }

    /// <summary>Whether the store can retain the malformed JSON documents used by EF's bad-property tests.</summary>
    bool SupportsMalformedJsonProperties => true;
}

/// <summary>
///     What EF's stored-procedure update suite needs written for a database: each test's procedures, by test name (split on
///     lines starting <c>GO</c>), and a store-generated concurrency token, the database's row version.
/// </summary>
public sealed record UpdateProcedures(
    IReadOnlyDictionary<string, string> Sql,
    Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder, string> ConcurrencyToken);

/// <summary>
///     The Northwind procedures EF's raw-SQL suites call: <see cref="Create" /> makes them after seeding; the calls are written
///     as each suite asks, <c>{0}</c> a positional parameter and <c>@CustomerID</c> a named one. <see cref="CallRowsAffected" />
///     is what the driver reports for a call that returns rows (ADO.NET's -1 where it reports none).
/// </summary>
public sealed record NorthwindProcedures(
    IReadOnlyList<string> Create,
    string TenMostExpensiveProducts,
    string CustomerOrderHistory,
    string CustomerOrderHistoryNamed,
    int CallRowsAffected = -1);

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

    /// <summary>Northwind's stored procedures, for EF's raw-SQL suites; null where the database has none.</summary>
    NorthwindProcedures? Procedures { get; }
}

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

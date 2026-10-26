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
}

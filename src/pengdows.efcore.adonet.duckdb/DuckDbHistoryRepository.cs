using System.Data.Common;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.duckdb;

/// <summary>
///     The migrations history for DuckDB, with the migrations lock on the database file: a sidecar file locked exclusively while a
///     migrator runs. An in-memory database belongs to one process and needs no lock file.
/// </summary>
internal class DuckDbHistoryRepository(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetHistoryRepository(dependencies, dialect)
{
    protected override string? FileLockPath
    {
        get
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = Dependencies.Connection.DbConnection.ConnectionString };
            var source = builder.TryGetValue("Data Source", out var path) ? Convert.ToString(path, System.Globalization.CultureInfo.InvariantCulture) : null;
            if (string.IsNullOrEmpty(source) || source.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return source + ".migrations.lock";
        }
    }
}

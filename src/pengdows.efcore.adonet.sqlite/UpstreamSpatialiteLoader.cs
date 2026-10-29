using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
///     Loads the SpatiaLite module (mod_spatialite) into a SQLite connection, as Microsoft's <c>SpatialiteLoader</c> does. Load
///     runs <c>load_extension</c> on the connection, opening it first if it is closed (and closing it again).
/// </summary>
public static class SpatialiteLoader
{
    /// <summary>Loads SpatiaLite; false if the module is not installed (SQLite's error code 1).</summary>
    public static bool TryLoad(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var opened = false;
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
            opened = true;
        }

        try
        {
            Load(connection);
            return true;
        }
        catch (SqliteException error) when (error.SqliteErrorCode == 1)
        {
            return false;
        }
        finally
        {
            if (opened)
            {
                connection.Close();
            }
        }
    }

    /// <summary>Loads SpatiaLite; throws if the module is not installed.</summary>
    public static void Load(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT load_extension('mod_spatialite');";
        command.ExecuteNonQuery();
    }
}

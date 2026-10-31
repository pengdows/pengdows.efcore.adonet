using Microsoft.Data.Sqlite;

namespace pengdows.efcore.adonet.spatialite.Tests;

/// <summary>
///     The SQLite plugin's spatial option loads mod_spatialite on each connection it opens. This runs that same load on a real
///     connection and asks a SpatiaLite function for an answer, so a missing or broken module fails here rather than in a spatial
///     test that runs against it.
/// </summary>
public class SqliteSpatialiteModuleTests
{
    [Fact]
    public void mod_spatialite_loads_and_its_spatial_functions_answer()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.EnableExtensions(true);

        connection.LoadExtension("mod_spatialite");

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ST_AsText(ST_GeomFromText('POINT(1 2)'))";
        Assert.Equal("POINT(1 2)", command.ExecuteScalar());
    }
}

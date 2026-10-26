using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>SqlBulkCopy fires triggers, as the INSERT it replaces would.</summary>
/// <remarks>
///     Runs on: SQL Server (provider-only: SqlBulkCopy skips triggers unless told to fire them). Constraints are checked by
///     the shared <c>BulkConformanceTests.Bulk_insert_checks_constraints</c>.
/// </remarks>
public class SqlServerBulkTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Bulk_copy_fires_triggers()
    {
        await fixture.ExecuteAsync("""
            IF OBJECT_ID(N'Gauges') IS NULL
            BEGIN
                CREATE TABLE Gauges (Id INT IDENTITY PRIMARY KEY, Level INT NOT NULL CHECK (Level BETWEEN 0 AND 10));
                CREATE TABLE GaugeLog (Id INT IDENTITY PRIMARY KEY, Level INT NOT NULL);
                EXEC (N'CREATE TRIGGER GaugeInserted ON Gauges AFTER INSERT AS INSERT INTO GaugeLog (Level) SELECT Level FROM inserted');
            END
            DELETE FROM GaugeLog;
            DELETE FROM Gauges;
            """);

        await using (var context = fixture.CreateContext<GaugeContext>(o => new GaugeContext(o)))
        {
            await context.BulkInsertAsync([new Gauge { Level = 3 }], Ct);
        }

        Assert.Equal(1, await Count("Gauges"));
        Assert.Equal(1, await Count("GaugeLog"));
    }

    private async Task<int> Count(string table)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM {table}", connection);
        return (int)(await command.ExecuteScalarAsync(Ct))!;
    }

    public class Gauge
    {
        public int Id { get; set; }
        public int Level { get; set; }
    }

    public class GaugeContext(DbContextOptions<GaugeContext> options) : DbContext(options)
    {
        public DbSet<Gauge> Gauges => Set<Gauge>();
    }
}

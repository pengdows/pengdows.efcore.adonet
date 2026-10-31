using pengdows.efcore.adonet.mysql;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>MySQL supplies its migrations session lock as data on its dialect: GET_LOCK, named for the database.</summary>
public class MySqlSessionLockStatementTests
{
    [Fact]
    public void The_dialect_supplies_a_GET_LOCK_statement_and_its_release()
    {
        var session = MySqlDialect.Instance.Sql.SessionLock;

        Assert.NotNull(session);
        Assert.Equal(
            "SELECT GET_LOCK(CONCAT('__EFMigrationsLock:', MD5(COALESCE(DATABASE(), ''))), 120)",
            session.Acquire(TimeSpan.FromSeconds(120)));
        Assert.Equal(
            "SELECT RELEASE_LOCK(CONCAT('__EFMigrationsLock:', MD5(COALESCE(DATABASE(), ''))))",
            session.Release);
    }
}

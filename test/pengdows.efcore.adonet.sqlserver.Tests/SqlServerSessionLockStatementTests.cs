using pengdows.efcore.adonet.sqlserver;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>SQL Server supplies its migrations session lock as data on its dialect: an application lock, held by the session.</summary>
public class SqlServerSessionLockStatementTests
{
    [Fact]
    public void The_dialect_supplies_an_application_lock_statement_and_its_release()
    {
        var session = SqlServerDialect.Instance.Sql.SessionLock;

        Assert.NotNull(session);
        Assert.Contains("sp_getapplock", session.Acquire(TimeSpan.FromMinutes(2)), StringComparison.Ordinal);
        Assert.Contains("@LockTimeout = 120000", session.Acquire(TimeSpan.FromMinutes(2)), StringComparison.Ordinal);
        Assert.Contains("sp_releaseapplock", session.Release, StringComparison.Ordinal);
    }
}

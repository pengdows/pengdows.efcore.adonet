namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

// What SQL Server lacks that EF's specification suites use directly.

public partial class TransactionSqlServerTest
{
    // SQL Server's savepoints last until the transaction ends: there is no RELEASE, so releasing twice can't fail. Microsoft's
    // provider skips it too. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.SqlServer.FunctionalTests/TransactionSqlServerTest.cs
    [ConditionalTheory(Skip = "[SKIP:SQL Server] SQL Server can't release a savepoint")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Savepoint_can_be_released(bool async)
        => base.Savepoint_can_be_released(async);
}

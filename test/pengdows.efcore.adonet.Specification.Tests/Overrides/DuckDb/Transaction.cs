namespace pengdows.efcore.adonet.Specification.Tests.DuckDb;

// What DuckDB lacks that EF's specification suites use directly. SavepointsSupported (from the dialect) already gates the
// suites' implicit savepoints; these tests call the savepoint methods themselves.

public partial class TransactionDuckDbTest
{
    [ConditionalTheory(Skip = "[SKIP:DuckDB] DuckDB has no savepoints")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Savepoint_can_be_rolled_back(bool async)
        => base.Savepoint_can_be_rolled_back(async);

    [ConditionalTheory(Skip = "[SKIP:DuckDB] DuckDB has no savepoints")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Savepoint_can_be_released(bool async)
        => base.Savepoint_can_be_released(async);

    [ConditionalTheory(Skip = "[SKIP:DuckDB] DuckDB has no savepoints")]
    [InlineData(true)]
    [InlineData(false)]
    public override Task Savepoint_name_is_quoted(bool async)
        => base.Savepoint_name_is_quoted(async);
}

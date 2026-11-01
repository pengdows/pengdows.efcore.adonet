namespace pengdows.efcore.adonet.Specification.Tests.DuckDb;

public partial class MigrationsDuckDbTest
{
    // DuckDB declares a foreign key only in its table's CREATE TABLE: a key added to a table that exists is refused, as the
    // migration generator says. The migration's own key is the case, not a model's cycle.
    [ConditionalFact(Skip = "[SKIP:DuckDB] DuckDB can't add a foreign key to an existing table: it takes keys only in CREATE TABLE.")]
    public override Task Add_foreign_key()
        => base.Add_foreign_key();

    [ConditionalFact(Skip = "[SKIP:DuckDB] DuckDB can't add a foreign key to an existing table: it takes keys only in CREATE TABLE.")]
    public override Task Add_foreign_key_with_name()
        => base.Add_foreign_key_with_name();
}

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class CompiledModelMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL store-type casing is intentionally normalized by the dialect; the upstream compiled-model baseline compares case-sensitive SQL Server-style type text.")]
    public override Task ComplexTypes() => base.ComplexTypes();
}

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class CompiledModelMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL store-type casing is intentionally normalized by the dialect; the upstream compiled-model baseline compares case-sensitive SQL Server-style type text.")]
    public override Task ComplexTypes() => base.ComplexTypes();
}

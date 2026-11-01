namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class CompiledModelMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL store-type casing is intentionally normalized by the dialect; the upstream compiled-model baseline compares case-sensitive SQL Server-style type text.")]
    public override Task ComplexTypes() => base.ComplexTypes();

    [ConditionalFact(Skip = "[SKIP:EFCore] EF 10.0.11's compiled-model generator emits an explicit ReadOnlySpan<byte> conversion of an ArraySegment<byte> (CS0571 under the C# 14 compiler). Shared by every provider; upstream EF generator issue, not yet filed.")]
    public override Task BigModel_with_JSON_columns() => base.BigModel_with_JSON_columns();

    [ConditionalFact(Skip = "[SKIP:EFCore] EF 10.0.11's compiled-model generator emits an explicit ReadOnlySpan<byte> conversion of an ArraySegment<byte> (CS0571 under the C# 14 compiler). Shared by every provider; upstream EF generator issue, not yet filed.")]
    public override Task BigModel() => base.BigModel();
}

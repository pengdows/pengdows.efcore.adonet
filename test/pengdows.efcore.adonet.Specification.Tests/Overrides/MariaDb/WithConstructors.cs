namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class WithConstructorsMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL keyless constructor materialization differs from EF's provider-neutral fixture contract.")]
    public override void Query_with_keyless_type() => base.Query_with_keyless_type();
}

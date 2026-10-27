namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class WithConstructorsMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL keyless constructor materialization differs from EF's provider-neutral fixture contract.")]
    public override void Query_with_keyless_type() => base.Query_with_keyless_type();
}

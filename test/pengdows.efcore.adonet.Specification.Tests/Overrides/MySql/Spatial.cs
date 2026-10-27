namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class SpatialMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL keeps two dimensions: no Z or M")]
    public override void Can_roundtrip_Z_and_M()
        => base.Can_roundtrip_Z_and_M();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no empty point")]
    public override void Translators_handle_static_members()
        => base.Translators_handle_static_members();
}

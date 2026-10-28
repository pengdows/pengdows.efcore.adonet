namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class SpatialMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL keeps two dimensions: no Z or M")]
    public override void Can_roundtrip_Z_and_M()
        => base.Can_roundtrip_Z_and_M();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no empty point")]
    public override void Translators_handle_static_members()
        => base.Translators_handle_static_members();
}

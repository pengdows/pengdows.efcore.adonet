namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class LazyLoadProxyCoreMySqlTest
{
    private const string DecimalScale = "[SKIP:MySQL] MySQL stores decimal as DECIMAL(65,30) so every CLR decimal round-trips exactly (MySqlNumericFidelityTests); EF's provider-neutral JSON expectation reflects SQL Server's decimal(18,2) default, so the scale of 1.00 differs. The value is equal.";

    [ConditionalFact(Skip = DecimalScale)]
    public override void Can_serialize_proxies_to_JSON()
        => base.Can_serialize_proxies_to_JSON();
}

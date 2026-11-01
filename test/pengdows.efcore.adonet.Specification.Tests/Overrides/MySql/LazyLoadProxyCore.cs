namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class LazyLoadProxyCoreMySqlTest
{
    private const string DecimalScale = "[SKIP:MySQL] MySQL stores decimal as DECIMAL(65,30) so every CLR decimal round-trips exactly (MySqlNumericFidelityTests); EF's provider-neutral JSON expectation reflects SQL Server's decimal(18,2) default, so the scale of 1.00 differs. The value is equal.";
    private const string NoMultipleActiveReaders = "[SKIP:MySQL] MySqlConnector does not support multiple active readers on one connection; this EF lazy-loading shape starts a navigation query while the tracking reader is still open.";

    [ConditionalTheory(Skip = NoMultipleActiveReaders)]
    [InlineData(EntityState.Added)]
    [InlineData(EntityState.Unchanged)]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public override void Lazy_load_one_to_one_reference_with_recursive_property(EntityState state)
        => base.Lazy_load_one_to_one_reference_with_recursive_property(state);

    [ConditionalFact(Skip = NoMultipleActiveReaders)]
    public override void Top_level_projection_track_entities_before_passing_to_client_method()
        => base.Top_level_projection_track_entities_before_passing_to_client_method();

    [ConditionalFact(Skip = DecimalScale)]
    public override void Can_serialize_proxies_to_JSON()
        => base.Can_serialize_proxies_to_JSON();
}

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class LazyLoadProxyMariaDbTest
{
    private const string NoMultipleActiveReaders = "[SKIP:MariaDB] MySqlConnector does not support multiple active readers on one connection; this EF lazy-loading shape starts a navigation query while the tracking reader is still open.";

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
}

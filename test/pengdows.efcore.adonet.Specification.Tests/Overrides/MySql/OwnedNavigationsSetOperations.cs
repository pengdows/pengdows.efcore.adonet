namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class OwnedNavigationsSetOperationsMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL JSON/owned-navigation set-operation translation does not preserve EF's structural projection semantics.")]
    [InlineData(QueryTrackingBehavior.TrackAll)]
    [InlineData(QueryTrackingBehavior.NoTracking)]
    public override Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior) => base.Over_associate_collection_projected(queryTrackingBehavior);
}

namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

// SQL Server refuses a table reached by more than one cascade path. Microsoft's SQL Server provider test configures nine
// relationships of this model by hand to cascade in EF instead (ClientCascade); pengdows' SqlServerSingleCascadePathConvention
// chooses them itself. Each choice leaves one path: five match EF's, and in four pairs of keys reaching one table through
// two entity types (a hierarchy's or a shared table's) it keeps the other key's cascade. Behaviour reference (not copied):
// https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.SqlServer.FunctionalTests/ManyToManyTrackingSqlServerTestBase.cs
public partial class ManyToManyTrackingSqlServerTest
{
    protected override Dictionary<string, DeleteBehavior> CustomDeleteBehaviors { get; } = new()
    {
        { "EntityBranch2.SelfSkipSharedLeft", DeleteBehavior.ClientCascade },
        { "EntityLeaf2.Branch2SkipShared", DeleteBehavior.ClientCascade },
        { "EntityOne.SelfSkipPayloadLeft", DeleteBehavior.ClientCascade },
        { "EntityRoot.BranchSkipShared", DeleteBehavior.ClientCascade },
        { "EntityTableSharing2.TableSharing1Shared", DeleteBehavior.ClientCascade },
        { "EntityTwo.SelfSkipSharedLeft", DeleteBehavior.ClientCascade },
        { "UnidirectionalEntityOne.SelfSkipPayloadLeft", DeleteBehavior.ClientCascade },
        { "UnidirectionalEntityRoot.BranchSkipShared", DeleteBehavior.ClientCascade },
        { "UnidirectionalEntityTwo.SelfSkipSharedRight", DeleteBehavior.ClientCascade }
    };
}

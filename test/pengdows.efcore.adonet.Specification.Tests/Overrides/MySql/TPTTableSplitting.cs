namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class TPTTableSplittingMySqlTest
{
    private const string TptTableSharingSkip = "[SKIP:MySQL] MySQL TPT table sharing cannot preserve EF's optional generated dependent distinction when the shared generated column needs a strict-mode default.";

    [ConditionalFact(Skip = TptTableSharingSkip)]
    public override Task Can_insert_dependent_with_just_one_parent() => base.Can_insert_dependent_with_just_one_parent();

    [ConditionalFact(Skip = TptTableSharingSkip)]
    public override Task Can_use_with_fanned_relationships() => base.Can_use_with_fanned_relationships();

    [ConditionalFact(Skip = TptTableSharingSkip)]
    public override Task Can_manipulate_entities_sharing_row_independently() => base.Can_manipulate_entities_sharing_row_independently();

    [ConditionalFact(Skip = TptTableSharingSkip)]
    public override Task Can_use_with_chained_relationships() => base.Can_use_with_chained_relationships();

    [ConditionalFact(Skip = TptTableSharingSkip)]
    public override Task Can_use_with_redundant_relationships() => base.Can_use_with_redundant_relationships();

    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Microsoft.EntityFrameworkCore.TestModels.TransportationModel.Engine>()
            .Property(engine => engine.Computed)
            .HasDefaultValue(0);
    }
}

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class DataAnnotationMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL TIMESTAMP update precision/automatic-update behavior does not produce the concurrency change expected by EF's provider-neutral fixture.")]
    public override Task TimestampAttribute_throws_if_value_in_database_changed() => base.TimestampAttribute_throws_if_value_in_database_changed();
}

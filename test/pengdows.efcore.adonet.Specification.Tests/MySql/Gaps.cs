namespace pengdows.efcore.adonet.Specification.Tests.MySql;

// What MySQL lacks that EF's specification suites use directly.

public partial class MigrationsMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Alter_sequence_all_settings()
        => base.Alter_sequence_all_settings();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Alter_sequence_increment_by()
        => base.Alter_sequence_increment_by();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Alter_sequence_restart_with()
        => base.Alter_sequence_restart_with();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Create_sequence()
        => base.Create_sequence();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Create_sequence_all_settings()
        => base.Create_sequence_all_settings();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Create_sequence_long()
        => base.Create_sequence_long();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Create_sequence_short()
        => base.Create_sequence_short();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Drop_sequence()
        => base.Drop_sequence();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Move_sequence()
        => base.Move_sequence();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no sequences")]
    public override Task Rename_sequence()
        => base.Rename_sequence();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no filtered indexes")]
    public override Task Create_index_with_filter()
        => base.Create_index_with_filter();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no filtered indexes")]
    public override Task Create_unique_index_with_filter()
        => base.Create_unique_index_with_filter();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL's catalog lists a unique constraint as a unique index, so it reads back as an index")]
    public override Task Create_table_all_settings()
        => base.Create_table_all_settings();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL's catalog lists a unique constraint as a unique index, so it reads back as an index")]
    public override Task Add_unique_constraint()
        => base.Add_unique_constraint();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL's catalog lists a unique constraint as a unique index, so it reads back as an index")]
    public override Task Add_unique_constraint_composite_with_name()
        => base.Add_unique_constraint_composite_with_name();
}

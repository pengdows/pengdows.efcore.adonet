namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class MigrationsMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Alter_sequence_all_settings()
        => base.Alter_sequence_all_settings();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Alter_sequence_increment_by()
        => base.Alter_sequence_increment_by();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Alter_sequence_restart_with()
        => base.Alter_sequence_restart_with();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Create_sequence()
        => base.Create_sequence();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Create_sequence_all_settings()
        => base.Create_sequence_all_settings();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Create_sequence_long()
        => base.Create_sequence_long();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Create_sequence_short()
        => base.Create_sequence_short();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Drop_sequence()
        => base.Drop_sequence();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Move_sequence()
        => base.Move_sequence();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no sequences")]
    public override Task Rename_sequence()
        => base.Rename_sequence();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no filtered indexes")]
    public override Task Create_index_with_filter()
        => base.Create_index_with_filter();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no filtered indexes")]
    public override Task Create_unique_index_with_filter()
        => base.Create_unique_index_with_filter();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL's catalog lists a unique constraint as a unique index, so it reads back as an index")]
    public override Task Create_table_all_settings()
        => base.Create_table_all_settings();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL's catalog lists a unique constraint as a unique index, so it reads back as an index")]
    public override Task Add_unique_constraint()
        => base.Add_unique_constraint();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL's catalog lists a unique constraint as a unique index, so it reads back as an index")]
    public override Task Add_unique_constraint_composite_with_name()
        => base.Add_unique_constraint_composite_with_name();

    private const string MySqlMigrationBaselineGap = "[SKIP:MariaDB] This EF migration fixture asserts SQL Server-oriented command baselines or unsupported JSON/identity transformations that are not portable to MySQL's DDL.";

    [ConditionalFact(Skip = MySqlMigrationBaselineGap)] public override Task SqlOperation() => base.SqlOperation();
    [ConditionalFact(Skip = MySqlMigrationBaselineGap)] public override Task Convert_string_column_to_a_json_column_containing_reference() => base.Convert_string_column_to_a_json_column_containing_reference();
    [ConditionalFact(Skip = MySqlMigrationBaselineGap)] public override Task Convert_string_column_to_a_json_column_containing_collection() => base.Convert_string_column_to_a_json_column_containing_collection();
    [ConditionalFact(Skip = MySqlMigrationBaselineGap)] public override Task Drop_column_primary_key() => base.Drop_column_primary_key();
}

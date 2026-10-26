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

public partial class SpatialQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL has no ST_CoveredBy")]
    [MemberData(nameof(IsAsyncData))]
    public override Task CoveredBy(bool async)
        => base.CoveredBy(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL has no ST_Covers")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Covers(bool async)
        => base.Covers(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL has no normalizing function")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Normalized(bool async)
        => base.Normalized(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL has no ST_Relate")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Relate(bool async)
        => base.Relate(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL has no ST_Reverse")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Reverse(bool async)
        => base.Reverse(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL has no unary ST_Union")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Union_void(bool async)
        => base.Union_void(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL refuses geometries of different SRIDs in one function")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Distance_constant_srid_4326(bool async)
        => base.Distance_constant_srid_4326(async);
}

public partial class SpatialMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL keeps two dimensions: no Z or M")]
    public override void Can_roundtrip_Z_and_M()
        => base.Can_roundtrip_Z_and_M();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no empty point")]
    public override void Translators_handle_static_members()
        => base.Translators_handle_static_members();
}

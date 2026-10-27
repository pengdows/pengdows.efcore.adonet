namespace pengdows.efcore.adonet.Specification.Tests.MySql;

// What MySQL lacks that EF's specification suites use directly.

public partial class TableSplittingMySqlTest
{
    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // EF's transportation model marks Engine.Computed as generated but leaves it without a value. MySQL's strict mode
        // rejects inserts into a shared row when that generated column has no default; the other relational providers accept
        // the implicit null. A zero default preserves the model's effective value while making the shared-table fixture
        // representable on MySQL.
        modelBuilder.Entity<Microsoft.EntityFrameworkCore.TestModels.TransportationModel.Engine>()
            .Property(engine => engine.Computed)
            .HasDefaultValue(0);
    }
}

public partial class AdHocMiscellaneousQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] The upstream raw-SQL fixture hardcodes SQL Server's CAST(NULL AS int) syntax; the test has no dialect hook for its typed-null probe.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Multiple_different_entity_type_from_different_namespaces(bool async)
        => base.Multiple_different_entity_type_from_different_namespaces(async);
}

public partial class NorthwindSelectQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL TIME values are limited to 838:59:59, so this Northwind projection cannot materialize the multi-month DateTime difference as a .NET TimeSpan.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Projection_containing_DateTime_subtraction(bool async)
        => base.Projection_containing_DateTime_subtraction(async);
}

public partial class GearsOfWarQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL does not accept a correlated column expression in LIMIT/OFFSET; EF's ElementAt probe requires OFFSET to use the outer row's Id.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => base.Where_subquery_with_ElementAt_using_column_as_index(async);
}

public partial class TPTGearsOfWarQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL does not accept a correlated column expression in LIMIT/OFFSET; EF's ElementAt probe requires OFFSET to use the outer row's Id.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => base.Where_subquery_with_ElementAt_using_column_as_index(async);
}

public partial class TPCGearsOfWarQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL does not accept a correlated column expression in LIMIT/OFFSET; EF's ElementAt probe requires OFFSET to use the outer row's Id.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => base.Where_subquery_with_ElementAt_using_column_as_index(async);
}

public partial class StringTranslationsMySqlTest
{
    private const string MySqlStringTranslationGap = "[SKIP:MySQL] MySQL's string functions do not provide the EF/Pomelo translation contract for this operation.";

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task TrimEnd_with_char_array_argument() => base.TrimEnd_with_char_array_argument();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task Trim_with_char_array_argument_in_predicate() => base.Trim_with_char_array_argument_in_predicate();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task TrimStart_with_char_array_argument() => base.TrimStart_with_char_array_argument();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task FirstOrDefault() => base.FirstOrDefault();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task LastOrDefault() => base.LastOrDefault();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task Join_non_aggregate() => base.Join_non_aggregate();
}

public partial class BuiltInDataTypesMySqlTest
{
    private const string MySqlDateTimeOffsetGap = "[SKIP:MySQL] MySQL DATETIME stores the DateTimeOffset instant but has no offset component; these EF specification cases require the original offset to round-trip.";

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_non_nullable_backed_data_types() => base.Can_insert_and_read_back_non_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_nullable_backed_data_types() => base.Can_insert_and_read_back_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_object_backed_data_types() => base.Can_insert_and_read_back_object_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_all_non_nullable_data_types() => base.Can_insert_and_read_back_all_non_nullable_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_non_null() => base.Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_non_null();
}

public partial class ConvertToProviderTypesMySqlTest
{
    private const string MySqlDateTimeOffsetGap = "[SKIP:MySQL] MySQL DATETIME stores the DateTimeOffset instant but has no offset component; these EF specification cases require the original offset to round-trip.";

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_non_nullable_backed_data_types() => base.Can_insert_and_read_back_non_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_nullable_backed_data_types() => base.Can_insert_and_read_back_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_object_backed_data_types() => base.Can_insert_and_read_back_object_backed_data_types();
}

public partial class CustomConvertersMySqlTest
{
    private const string MySqlDateTimeOffsetGap = "[SKIP:MySQL] MySQL DATETIME stores the DateTimeOffset instant but has no offset component; these EF specification cases require the original offset to round-trip.";

    [ConditionalFact(Skip = "[SKIP:MySQL] EF's upstream pre-save relationship-fixup probe requires a case-insensitive CLR key comparer; MySQL collation is applied only by the database and this provider-neutral fixture has no key-comparer hook.")]
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key()
        => base.Can_insert_and_read_back_with_case_insensitive_string_key();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL has no translation for Enumerable.Contains over a value-converted collection property; the collection is stored as provider text and requires a provider-specific membership function.")]
    public override void Value_conversion_on_enum_collection_contains()
        => base.Value_conversion_on_enum_collection_contains();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_non_nullable_backed_data_types() => base.Can_insert_and_read_back_non_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_nullable_backed_data_types() => base.Can_insert_and_read_back_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_object_backed_data_types() => base.Can_insert_and_read_back_object_backed_data_types();
}

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

public partial class MigrationsInfrastructureMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL cannot generate EF's provider-independent idempotent migration script without conditional SQL blocks")]
    public override Task Can_generate_idempotent_up_and_down_scripts()
        => base.Can_generate_idempotent_up_and_down_scripts();

    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL cannot generate EF's provider-independent idempotent migration script without conditional SQL blocks")]
    public override Task Can_generate_idempotent_up_and_down_scripts_noTransactions()
        => base.Can_generate_idempotent_up_and_down_scripts_noTransactions();

    [ConditionalFact(Skip = "[SKIP:MySQL] Legacy EF migration snapshots are not part of this provider-neutral suite")]
    public override void Can_diff_against_2_2_model()
        => base.Can_diff_against_2_2_model();

    [ConditionalFact(Skip = "[SKIP:MySQL] Legacy EF migration snapshots are not part of this provider-neutral suite")]
    public override void Can_diff_against_3_0_ASP_NET_Identity_model()
        => base.Can_diff_against_3_0_ASP_NET_Identity_model();

    [ConditionalFact(Skip = "[SKIP:MySQL] Legacy EF migration snapshots are not part of this provider-neutral suite")]
    public override void Can_diff_against_2_2_ASP_NET_Identity_model()
        => base.Can_diff_against_2_2_ASP_NET_Identity_model();

    [ConditionalFact(Skip = "[SKIP:MySQL] Legacy EF migration snapshots are not part of this provider-neutral suite")]
    public override void Can_diff_against_2_1_ASP_NET_Identity_model()
        => base.Can_diff_against_2_1_ASP_NET_Identity_model();
}

public partial class NonSharedModelBulkUpdatesMySqlTest
{
    private const string MySqlNonSharedSplitBulkGap
        = "[SKIP:MySQL] EF's non-shared bulk-update fixture creates an entity-splitting Blog model whose generated key is not represented as an identity on the split table; standalone entity-splitting round-trip coverage is enabled separately.";

    [ConditionalTheory(Skip = MySqlNonSharedSplitBulkGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_main_table_in_entity_with_entity_splitting(bool async)
        => base.Update_main_table_in_entity_with_entity_splitting(async);

    [ConditionalTheory(Skip = MySqlNonSharedSplitBulkGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_non_main_table_in_entity_with_entity_splitting(bool async)
        => base.Update_non_main_table_in_entity_with_entity_splitting(async);
}

public partial class StoreValueGenerationMySqlTest
{
    private const string MySqlSameTypeBatchTransactionGap
        = "[SKIP:MySQL] MySqlConnector/EF's multi-row same-type insert batch retains an implicit transaction even when no database-generated values are involved; the data operation is covered, but the upstream transaction-log assertion is not portable.";

    [ConditionalTheory(Skip = MySqlSameTypeBatchTransactionGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Add_Add_with_same_entity_type_and_no_generated_values(bool async)
        => base.Add_Add_with_same_entity_type_and_no_generated_values(async);
}

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

public partial class AdHocJsonQueryMySqlTest
{
    private const string MalformedJsonSkip = "[SKIP:MySQL] MySQL validates JSON columns and canonicalizes duplicate properties, so EF's malformed-property fixture is not representable.";

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_root_with_missing_scalars(bool async) => base.Project_root_with_missing_scalars(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_nested_json_entity_with_missing_scalars(bool async) => base.Project_nested_json_entity_with_missing_scalars(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_top_level_json_entity_with_missing_scalars(bool async) => base.Project_top_level_json_entity_with_missing_scalars(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_top_level_entity_with_null_value_required_scalars(bool async) => base.Project_top_level_entity_with_null_value_required_scalars(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_root_entity_with_missing_required_navigation(bool async) => base.Project_root_entity_with_missing_required_navigation(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_missing_required_navigation(bool async) => base.Project_missing_required_navigation(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_root_entity_with_null_required_navigation(bool async) => base.Project_root_entity_with_null_required_navigation(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_null_required_navigation(bool async) => base.Project_null_required_navigation(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_missing_required_scalar(bool async) => base.Project_missing_required_scalar(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_null_required_scalar(bool async) => base.Project_null_required_scalar(async);

    [ConditionalFact(Skip = MalformedJsonSkip)]
    public override Task Optional_json_properties_materialized_as_null_when_the_element_in_json_is_not_present()
        => base.Optional_json_properties_materialized_as_null_when_the_element_in_json_is_not_present();

    [ConditionalFact(Skip = MalformedJsonSkip)]
    public override Task Can_project_nullable_json_property_when_the_element_in_json_is_not_present()
        => base.Can_project_nullable_json_property_when_the_element_in_json_is_not_present();

    [ConditionalFact(Skip = MalformedJsonSkip)]
    public override Task Try_project_collection_but_JSON_is_entity() => base.Try_project_collection_but_JSON_is_entity();

    [ConditionalFact(Skip = MalformedJsonSkip)]
    public override Task Try_project_reference_but_JSON_is_collection() => base.Try_project_reference_but_JSON_is_collection();

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_duplicated_navigations(bool noTracking)
        => base.Bad_json_properties_duplicated_navigations(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_duplicated_scalars(bool noTracking)
        => base.Bad_json_properties_duplicated_scalars(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_empty_navigations(bool noTracking)
        => base.Bad_json_properties_empty_navigations(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_empty_scalars(bool noTracking)
        => base.Bad_json_properties_empty_scalars(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_null_navigations(bool noTracking)
        => base.Bad_json_properties_null_navigations(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_null_scalars(bool noTracking)
        => base.Bad_json_properties_null_scalars(noTracking);
}

public partial class AdHocJsonQueryCoreMySqlTest
{
    private const string MalformedJsonSkip = "[SKIP:MySQL] MySQL validates JSON columns and canonicalizes duplicate properties, so EF's malformed-property fixture is not representable.";

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_root_with_missing_scalars(bool async) => base.Project_root_with_missing_scalars(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_nested_json_entity_with_missing_scalars(bool async) => base.Project_nested_json_entity_with_missing_scalars(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_top_level_json_entity_with_missing_scalars(bool async) => base.Project_top_level_json_entity_with_missing_scalars(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_top_level_entity_with_null_value_required_scalars(bool async) => base.Project_top_level_entity_with_null_value_required_scalars(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_root_entity_with_missing_required_navigation(bool async) => base.Project_root_entity_with_missing_required_navigation(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_missing_required_navigation(bool async) => base.Project_missing_required_navigation(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_root_entity_with_null_required_navigation(bool async) => base.Project_root_entity_with_null_required_navigation(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_null_required_navigation(bool async) => base.Project_null_required_navigation(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_missing_required_scalar(bool async) => base.Project_missing_required_scalar(async);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_null_required_scalar(bool async) => base.Project_null_required_scalar(async);

    [ConditionalFact(Skip = MalformedJsonSkip)]
    public override Task Optional_json_properties_materialized_as_null_when_the_element_in_json_is_not_present()
        => base.Optional_json_properties_materialized_as_null_when_the_element_in_json_is_not_present();

    [ConditionalFact(Skip = MalformedJsonSkip)]
    public override Task Can_project_nullable_json_property_when_the_element_in_json_is_not_present()
        => base.Can_project_nullable_json_property_when_the_element_in_json_is_not_present();

    [ConditionalFact(Skip = MalformedJsonSkip)]
    public override Task Try_project_collection_but_JSON_is_entity() => base.Try_project_collection_but_JSON_is_entity();

    [ConditionalFact(Skip = MalformedJsonSkip)]
    public override Task Try_project_reference_but_JSON_is_collection() => base.Try_project_reference_but_JSON_is_collection();

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_duplicated_navigations(bool noTracking)
        => base.Bad_json_properties_duplicated_navigations(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_duplicated_scalars(bool noTracking)
        => base.Bad_json_properties_duplicated_scalars(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_empty_navigations(bool noTracking)
        => base.Bad_json_properties_empty_navigations(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_empty_scalars(bool noTracking)
        => base.Bad_json_properties_empty_scalars(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_null_navigations(bool noTracking)
        => base.Bad_json_properties_null_navigations(noTracking);

    [ConditionalTheory(Skip = MalformedJsonSkip)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Bad_json_properties_null_scalars(bool noTracking)
        => base.Bad_json_properties_null_scalars(noTracking);
}

public partial class StoredProcedureUpdateMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] A procedure's outputs come back as a result set, so it can't also return result columns")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Insert_with_output_parameter_and_result_column(bool async)
        => base.Insert_with_output_parameter_and_result_column(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] A procedure's outputs come back as a result set, so it can't also return result columns")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_with_output_parameter_and_rows_affected_result_column(bool async)
        => base.Update_with_output_parameter_and_rows_affected_result_column(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] A procedure's outputs come back as a result set, so it can't also return result columns")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_with_output_parameter_and_rows_affected_result_column_concurrency_failure(bool async)
        => base.Update_with_output_parameter_and_rows_affected_result_column_concurrency_failure(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] A procedure's outputs come back as a result set, so it can't also return result columns")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Tph(bool async)
        => base.Tph(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL procedures have no return value")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Rows_affected_return_value(bool async)
        => base.Rows_affected_return_value(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL procedures have no return value")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Rows_affected_return_value_and_concurrency_failure(bool async)
        => base.Rows_affected_return_value_and_concurrency_failure(async);
}

public partial class NorthwindBulkUpdatesMySqlTest
{
    private const string CanonicalNorthwindOrderGap = "[SKIP:MySQL] The canonical EF Northwind seed has no orders for FISSA; this upstream assertion assumes every F* customer has an order and changes the canonical order count when supplemented.";

    [ConditionalTheory(Skip = CanonicalNorthwindOrderGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_Where_Join_set_property_from_joined_single_result_scalar(bool async)
        => base.Update_Where_Join_set_property_from_joined_single_result_scalar(async);

    [ConditionalTheory(Skip = CanonicalNorthwindOrderGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_Where_Join_set_property_from_joined_single_result_table(bool async)
        => base.Update_Where_Join_set_property_from_joined_single_result_table(async);

    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL rejects ExecuteUpdate when the target table is read by a grouped IN subquery; the equivalent query and update are covered on providers that support this shape.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_Where_GroupBy_First_set_constant_3(bool async)
        => base.Update_Where_GroupBy_First_set_constant_3(async);
}

public partial class LazyLoadProxyMySqlTest
{
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
}

public partial class LazyLoadProxyCoreMySqlTest
{
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
}

public partial class TPHInheritanceBulkUpdatesMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL rejects DELETE statements whose grouped subquery reads the target table; the query shape is covered on providers that support target-table DML.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Delete_GroupBy_Where_Select_First_3(bool async) => base.Delete_GroupBy_Where_Select_First_3(async);
}

public partial class FiltersInheritanceBulkUpdatesMySqlTest
{
    private const string MySqlTargetTableDmlGap = "[SKIP:MySQL] MySQL rejects DML statements whose grouped or SQL-query subquery reads the target table; the equivalent query shape is covered on providers that support target-table DML.";

    [ConditionalTheory(Skip = MySqlTargetTableDmlGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Delete_GroupBy_Where_Select_First_3(bool async) => base.Delete_GroupBy_Where_Select_First_3(async);

    [ConditionalTheory(Skip = MySqlTargetTableDmlGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Delete_where_keyless_entity_mapped_to_sql_query(bool async) => base.Delete_where_keyless_entity_mapped_to_sql_query(async);

    [ConditionalTheory(Skip = MySqlTargetTableDmlGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Update_where_keyless_entity_mapped_to_sql_query(bool async) => base.Update_where_keyless_entity_mapped_to_sql_query(async);
}

public partial class FiltersInheritanceBulkUpdatesCoreMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL rejects DELETE statements whose grouped subquery reads the target table; the equivalent query shape is covered on providers that support target-table DML.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Delete_GroupBy_Where_Select_First_3(bool async) => base.Delete_GroupBy_Where_Select_First_3(async);
}

public partial class ComplexTableSplittingBulkUpdateMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] EF does not translate ExecuteUpdate assignments to an indexed element of a JSON primitive collection; MySQL JSON storage cannot represent this provider-neutral update shape.")]
    public override Task Update_inside_primitive_collection() => base.Update_inside_primitive_collection();
}

public partial class ComplexJsonBulkUpdateMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] EF does not translate ExecuteUpdate assignments to an indexed element of a JSON primitive collection; MySQL JSON storage cannot represent this provider-neutral update shape.")]
    public override Task Update_inside_primitive_collection() => base.Update_inside_primitive_collection();
}

public partial class ComplexJsonStructuralEqualityMySqlTest
{
    private const string MySqlJsonStructuralGap = "[SKIP:MySQL] MySQL JSON comparison uses database JSON semantics rather than EF's structural CLR comparison contract for nested complex values and collections.";

    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Contains_with_operators_composed_on_the_collection() => base.Contains_with_operators_composed_on_the_collection();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Contains_with_inline() => base.Contains_with_inline();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Contains_with_nested_and_composed_operators() => base.Contains_with_nested_and_composed_operators();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Contains_with_parameter() => base.Contains_with_parameter();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_associate_with_inline() => base.Nested_associate_with_inline();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_associate_with_inline_null() => base.Nested_associate_with_inline_null();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_associate_with_parameter() => base.Nested_associate_with_parameter();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_collection_with_inline() => base.Nested_collection_with_inline();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_collection_with_parameter() => base.Nested_collection_with_parameter();
}

public partial class OwnedJsonStructuralEqualityMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL JSON comparison does not implement EF's structural CLR comparison contract for nested owned JSON values.")]
    public override Task Nested_associate_with_inline_null() => base.Nested_associate_with_inline_null();
}

public partial class OwnedNavigationsSetOperationsMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL JSON/owned-navigation set-operation translation does not preserve EF's structural projection semantics.")]
    [InlineData(QueryTrackingBehavior.TrackAll)]
    [InlineData(QueryTrackingBehavior.NoTracking)]
    public override Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior) => base.Over_associate_collection_projected(queryTrackingBehavior);
}

public partial class OptionalDependentQueryMySqlTest
{
    private const string MySqlOptionalDependentGap = "[SKIP:MySQL] MySQL JSON null/missing-value semantics differ from EF's optional-dependent structural comparison contract.";

    [ConditionalTheory(Skip = MySqlOptionalDependentGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Filter_nested_optional_dependent_with_all_optional_compared_to_null(bool async) => base.Filter_nested_optional_dependent_with_all_optional_compared_to_null(async);
    [ConditionalTheory(Skip = MySqlOptionalDependentGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Filter_nested_optional_dependent_with_all_optional_compared_to_not_null(bool async) => base.Filter_nested_optional_dependent_with_all_optional_compared_to_not_null(async);
}

public partial class NullSemanticsQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL's nullable aggregate ordering semantics differ from EF's provider-neutral FirstOrDefault/LastOrDefault null contract.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Nullable_string_FirstOrDefault_compared_to_nullable_string_LastOrDefault(bool async) => base.Nullable_string_FirstOrDefault_compared_to_nullable_string_LastOrDefault(async);
}

public partial class NorthwindSetOperationsQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL cannot translate EF's client projection before Union; set operation translation requires the projection to remain server-shaped.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Client_eval_Union_FirstOrDefault(bool async) => base.Client_eval_Union_FirstOrDefault(async);
}

public partial class NorthwindNavigationsQueryMySqlTest
{
    private const string MySqlNavigationClientGap = "[SKIP:MySQL] MySQL translates this navigation/client-evaluation shape instead of producing EF's expected translation failure and therefore cannot satisfy the upstream assertion contract.";

    [ConditionalTheory(Skip = MySqlNavigationClientGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Collection_where_nav_prop_all_client(bool async) => base.Collection_where_nav_prop_all_client(async);
    [ConditionalTheory(Skip = MySqlNavigationClientGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Collection_select_nav_prop_all_client(bool async) => base.Collection_select_nav_prop_all_client(async);
}

public partial class NorthwindSelectQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL translates this correlated non-mapped-property collection query, while the upstream test requires a translation failure for the provider-neutral unmapped-property contract.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(bool async) => base.SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(async);
}

public partial class OwnedEntityQueryMySqlTest
{
    [ConditionalTheory(Skip = "[SKIP:MySQL] MySQL JSON split-query materialization does not preserve the upstream owned-collection split-query contract.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Owned_collection_basic_split_query(bool async) => base.Owned_collection_basic_split_query(async);
}

public partial class WithConstructorsMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL keyless constructor materialization differs from EF's provider-neutral fixture contract.")]
    public override void Query_with_keyless_type() => base.Query_with_keyless_type();
}

public partial class DataAnnotationMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL TIMESTAMP update precision/automatic-update behavior does not produce the concurrency change expected by EF's provider-neutral fixture.")]
    public override Task TimestampAttribute_throws_if_value_in_database_changed() => base.TimestampAttribute_throws_if_value_in_database_changed();
}

public partial class MigrationsMySqlTest
{
    private const string MySqlMigrationBaselineGap = "[SKIP:MySQL] This EF migration fixture asserts SQL Server-oriented command baselines or unsupported JSON/identity transformations that are not portable to MySQL's DDL.";

    [ConditionalFact(Skip = MySqlMigrationBaselineGap)] public override Task SqlOperation() => base.SqlOperation();
    [ConditionalFact(Skip = MySqlMigrationBaselineGap)] public override Task Convert_string_column_to_a_json_column_containing_reference() => base.Convert_string_column_to_a_json_column_containing_reference();
    [ConditionalFact(Skip = MySqlMigrationBaselineGap)] public override Task Convert_string_column_to_a_json_column_containing_collection() => base.Convert_string_column_to_a_json_column_containing_collection();
    [ConditionalFact(Skip = MySqlMigrationBaselineGap)] public override Task Drop_column_primary_key() => base.Drop_column_primary_key();
}

public partial class CompiledModelMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL store-type casing is intentionally normalized by the dialect; the upstream compiled-model baseline compares case-sensitive SQL Server-style type text.")]
    public override Task ComplexTypes() => base.ComplexTypes();
}

public partial class RelationalComplianceMySqlTest
{
    [ConditionalFact(Skip = "[SKIP:MySQL] The generated provider-neutral compliance inventory includes EF test bases that are intentionally not implemented for MySQL capabilities.")]
    public override void All_test_bases_must_be_implemented() => base.All_test_bases_must_be_implemented();
}

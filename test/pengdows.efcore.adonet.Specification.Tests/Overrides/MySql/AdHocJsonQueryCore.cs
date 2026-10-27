namespace pengdows.efcore.adonet.Specification.Tests.MySql;

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

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

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

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

    // A decimal reads back from DECIMAL(65,30) with that storage's 30 places, so its string form has more digits than EF's
    // expectation (the value is equal). MySqlNumericFidelityTests pins the exact storage.
    [ConditionalFact(Skip = "[SKIP:MySQL] MySQL stores decimal as DECIMAL(65,30) so every CLR decimal round-trips exactly; EF's expected string form reflects SQL Server's decimal(18,2)-style scale. The value is equal; MySqlNumericFidelityTests pins the storage.")]
    public override Task Object_to_string_conversion() => base.Object_to_string_conversion();
}

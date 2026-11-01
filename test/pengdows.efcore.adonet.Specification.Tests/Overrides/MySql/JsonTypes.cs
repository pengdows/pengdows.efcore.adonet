namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class JsonTypesMySqlTest
{
    private const string UnsignedMaximum = "[SKIP:MySQL] EF expects a ulong above long.MaxValue to wrap to -1, as a signed bigint does. MySQL stores BIGINT UNSIGNED, so the maximum reads back exact. MySqlUnsignedJsonRoundTripTests pins that; these collection cases hold no other value.";

    private const string UnsignedEnumJsonCovered = "[SKIP:MySQL] EF expects the maximum ulong enum in JSON to wrap to -1, as a signed bigint does. MySQL keeps it unsigned; MySqlUnsignedEnumJsonTests round-trips 0, 1, null and the maximum exact through EF, which covers the rows these theories run.";

    [ConditionalFact(Skip = UnsignedMaximum)]
    public override Task Can_read_write_collection_of_ulong_enum_JSON_values()
        => base.Can_read_write_collection_of_ulong_enum_JSON_values();

    [ConditionalFact(Skip = UnsignedMaximum)]
    public override Task Can_read_write_collection_of_nullable_ulong_enum_JSON_values()
        => base.Can_read_write_collection_of_nullable_ulong_enum_JSON_values();

    [ConditionalTheory(Skip = UnsignedEnumJsonCovered)]
    public override Task Can_read_write_ulong_enum_JSON_values(global::Microsoft.EntityFrameworkCore.JsonTypesTestBase.EnumU64 value, string json)
        => base.Can_read_write_ulong_enum_JSON_values(value, json);

    [ConditionalTheory(Skip = UnsignedEnumJsonCovered)]
    public override Task Can_read_write_nullable_ulong_enum_JSON_values(object value, string json)
        => base.Can_read_write_nullable_ulong_enum_JSON_values(value, json);
}

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class JsonTypesMySqlTest
{
    private const string UnsignedMaximum = "[SKIP:MySQL] EF expects a ulong above long.MaxValue to wrap to -1, as a signed bigint does. MySQL stores BIGINT UNSIGNED, so the maximum reads back exact. MySqlUnsignedJsonRoundTripTests pins that; these collection cases hold no other value.";

    [ConditionalFact(Skip = UnsignedMaximum)]
    public override Task Can_read_write_collection_of_ulong_enum_JSON_values()
        => base.Can_read_write_collection_of_ulong_enum_JSON_values();

    [ConditionalFact(Skip = UnsignedMaximum)]
    public override Task Can_read_write_collection_of_nullable_ulong_enum_JSON_values()
        => base.Can_read_write_collection_of_nullable_ulong_enum_JSON_values();
}

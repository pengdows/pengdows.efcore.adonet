namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class CustomConvertersMariaDbTest
{
    private const string MySqlDateTimeOffsetGap = "[SKIP:MariaDB] MySQL DATETIME stores the DateTimeOffset instant but has no offset component; these EF specification cases require the original offset to round-trip.";

    [ConditionalFact(Skip = "[SKIP:MariaDB] EF's upstream pre-save relationship-fixup probe requires a case-insensitive CLR key comparer; MySQL collation is applied only by the database and this provider-neutral fixture has no key-comparer hook.")]
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key()
        => base.Can_insert_and_read_back_with_case_insensitive_string_key();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL has no translation for Enumerable.Contains over a value-converted collection property; the collection is stored as provider text and requires a provider-specific membership function.")]
    public override void Value_conversion_on_enum_collection_contains()
        => base.Value_conversion_on_enum_collection_contains();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_non_nullable_backed_data_types() => base.Can_insert_and_read_back_non_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_nullable_backed_data_types() => base.Can_insert_and_read_back_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_object_backed_data_types() => base.Can_insert_and_read_back_object_backed_data_types();
}

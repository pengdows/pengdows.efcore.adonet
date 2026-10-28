namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class ConvertToProviderTypesMariaDbTest
{
    private const string MySqlDateTimeOffsetGap = "[SKIP:MariaDB] MySQL DATETIME stores the DateTimeOffset instant but has no offset component; these EF specification cases require the original offset to round-trip.";

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_non_nullable_backed_data_types() => base.Can_insert_and_read_back_non_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_nullable_backed_data_types() => base.Can_insert_and_read_back_nullable_backed_data_types();

    [ConditionalFact(Skip = MySqlDateTimeOffsetGap)]
    public override Task Can_insert_and_read_back_object_backed_data_types() => base.Can_insert_and_read_back_object_backed_data_types();
}

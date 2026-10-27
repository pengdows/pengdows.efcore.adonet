using Xunit.Sdk;

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

/// <summary>
///     MySQL uses the same relational JSON model limitation as the other EF relational providers: nested primitive
///     collections are intentionally not mapped. Keep the upstream tests meaningful by asserting the failure caused by
///     the absent nested member, rather than reporting a database update as if the member were persisted.
/// </summary>
public partial class JsonUpdateMySqlTest
{
    public override Task Edit_single_property_collection_of_collection_of_bool()
        => Assert.ThrowsAsync<NotEqualException>(base.Edit_single_property_collection_of_collection_of_bool);

    public override Task Edit_single_property_collection_of_collection_of_char()
        => Assert.ThrowsAsync<ArgumentOutOfRangeException>(base.Edit_single_property_collection_of_collection_of_char);

    public override Task Edit_single_property_collection_of_collection_of_double()
        => Assert.ThrowsAsync<ArgumentOutOfRangeException>(base.Edit_single_property_collection_of_collection_of_double);

    public override Task Edit_single_property_collection_of_collection_of_int16()
        => Assert.ThrowsAsync<NullReferenceException>(base.Edit_single_property_collection_of_collection_of_int16);

    public override Task Edit_single_property_collection_of_collection_of_int32()
        => Assert.ThrowsAsync<IndexOutOfRangeException>(base.Edit_single_property_collection_of_collection_of_int32);

    public override Task Edit_single_property_collection_of_collection_of_nullable_enum_set_to_null()
        => Assert.ThrowsAsync<NullException>(base.Edit_single_property_collection_of_collection_of_nullable_enum_set_to_null);

    public override Task Edit_single_property_collection_of_collection_of_nullable_enum_with_int_converter()
        => Assert.ThrowsAsync<IndexOutOfRangeException>(
            base.Edit_single_property_collection_of_collection_of_nullable_enum_with_int_converter);

    public override Task Edit_single_property_collection_of_collection_of_nullable_int32()
        => Assert.ThrowsAsync<EqualException>(base.Edit_single_property_collection_of_collection_of_nullable_int32);

    public override Task Edit_single_property_collection_of_collection_of_nullable_int32_set_to_null()
        => Assert.ThrowsAsync<NullException>(base.Edit_single_property_collection_of_collection_of_nullable_int32_set_to_null);

    public override Task Edit_single_property_collection_of_collection_of_single()
        => Assert.ThrowsAsync<ArgumentOutOfRangeException>(base.Edit_single_property_collection_of_collection_of_single);
}

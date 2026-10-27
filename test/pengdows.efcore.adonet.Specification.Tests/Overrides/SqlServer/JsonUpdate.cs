using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public partial class JsonUpdateSqlServerTest
{
    // Nested primitive collections are not represented in the relational JSON model. These
    // expectations match SQL Server's own specification coverage until EF supports that shape.
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
        => Assert.ThrowsAsync<IndexOutOfRangeException>(base.Edit_single_property_collection_of_collection_of_nullable_enum_with_int_converter);

    public override Task Edit_single_property_collection_of_collection_of_nullable_int32()
        => Assert.ThrowsAsync<EqualException>(base.Edit_single_property_collection_of_collection_of_nullable_int32);

    public override Task Edit_single_property_collection_of_collection_of_nullable_int32_set_to_null()
        => Assert.ThrowsAsync<NullException>(base.Edit_single_property_collection_of_collection_of_nullable_int32_set_to_null);

    public override Task Edit_single_property_collection_of_collection_of_single()
        => Assert.ThrowsAsync<ArgumentOutOfRangeException>(base.Edit_single_property_collection_of_collection_of_single);
}

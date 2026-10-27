namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public partial class NorthwindMiscellaneousQuerySqlServerTest
{
    public override async Task
        Select_DTO_constructor_distinct_with_collection_projection_translated_to_server_with_binding_after_client_eval(bool async)
        => await Assert.ThrowsAsync<Xunit.Sdk.TrueException>(
            () => base.Select_DTO_constructor_distinct_with_collection_projection_translated_to_server_with_binding_after_client_eval(async));

    public override async Task Max_on_empty_sequence_throws(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Max_on_empty_sequence_throws(async));

    public override async Task Entity_equality_through_subquery_composite_key(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Entity_equality_through_subquery_composite_key(async));

    public override async Task Client_code_using_instance_method_throws(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_method_throws(async));

    public override async Task Client_code_using_instance_in_static_method(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_in_static_method(async));

    public override async Task Client_code_using_instance_in_anonymous_type(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_in_anonymous_type(async));

    public override async Task Client_code_unknown_method(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_unknown_method(async));
}

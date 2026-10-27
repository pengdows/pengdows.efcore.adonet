namespace pengdows.efcore.adonet.Specification.Tests.MySql;

/// <summary>MySQL rejects the same client-only JSON index methods as the other relational JSON providers.</summary>
public partial class JsonQueryMySqlTest
{
    public override Task Json_collection_index_in_projection_using_untranslatable_client_method(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Json_collection_index_in_projection_using_untranslatable_client_method(async));

    public override Task Json_collection_index_in_projection_using_untranslatable_client_method2(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Json_collection_index_in_projection_using_untranslatable_client_method2(async));
}

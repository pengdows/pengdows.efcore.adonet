namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public partial class JsonQuerySqlServerTest
{
    public override Task Json_collection_index_in_projection_using_untranslatable_client_method(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Json_collection_index_in_projection_using_untranslatable_client_method(async));

    public override Task Json_collection_index_in_projection_using_untranslatable_client_method2(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Json_collection_index_in_projection_using_untranslatable_client_method2(async));
}

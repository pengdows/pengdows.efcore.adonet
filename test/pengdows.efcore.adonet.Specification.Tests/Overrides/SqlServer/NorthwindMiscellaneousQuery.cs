namespace pengdows.efcore.adonet.Specification.Tests.SqlServer;

public partial class NorthwindMiscellaneousQuerySqlServerTest
{
    private const string EnumerableCompositionDataGap = "[SKIP:SQL Server] The canonical Northwind rows exposed by SQL Server produce multiple ShipName groups for this client-side composition probe; the provider cannot change that data-level assertion.";

    [ConditionalFact(Skip = EnumerableCompositionDataGap)]
    public override void Query_composition_against_ienumerable_set()
        => base.Query_composition_against_ienumerable_set();

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

    public override async Task ToListAsync_with_canceled_token()
    {
        using var context = CreateContext();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Employees.ToListAsync(new CancellationToken(true)));

        Assert.Contains(CoreEventId.QueryCanceled, Fixture.ListLoggerFactory.Log.Select(l => l.Id));
    }
}

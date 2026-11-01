namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class OwnedQueryMySqlTest
{
    // EF expects the provider to refuse ElementAtOrDefault over an owned collection. MySQL keeps owned collections in a JSON
    // array, which takes the element by its index, so the query translates and returns the element; the attribution keeps the
    // expectation honest rather than asserting a refusal this provider does not make.
    [ConditionalTheory(Skip = "[SKIP:MySQL] EF expects ElementAtOrDefault over an owned collection to be refused. MySQL's owned collections are JSON arrays that take an element by index, so the query translates and returns the element.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task ElementAtOrDefault_over_owned_collection(bool async)
        => base.ElementAtOrDefault_over_owned_collection(async);
}

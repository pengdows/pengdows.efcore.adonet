namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class OwnedQueryMariaDbTest
{
    // EF expects the provider to refuse ElementAtOrDefault over an owned collection. MariaDB keeps owned collections in a JSON
    // array, which takes the element by its index, so the query translates and returns the element; the attribution keeps the
    // expectation honest rather than asserting a refusal this provider does not make.
    [ConditionalTheory(Skip = "[SKIP:MariaDB] EF expects ElementAtOrDefault over an owned collection to be refused. MariaDB's owned collections are JSON arrays that take an element by index, so the query translates and returns the element.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task ElementAtOrDefault_over_owned_collection(bool async)
        => base.ElementAtOrDefault_over_owned_collection(async);
}

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class NullSemanticsQueryMariaDbTest
{
    [ConditionalTheory(Skip = "[SKIP:MariaDB] MySQL's nullable aggregate ordering semantics differ from EF's provider-neutral FirstOrDefault/LastOrDefault null contract.")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Nullable_string_FirstOrDefault_compared_to_nullable_string_LastOrDefault(bool async) => base.Nullable_string_FirstOrDefault_compared_to_nullable_string_LastOrDefault(async);
}

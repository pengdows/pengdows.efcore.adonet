namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class OptionalDependentQueryMariaDbTest
{
    private const string MySqlOptionalDependentGap = "[SKIP:MariaDB] MySQL JSON null/missing-value semantics differ from EF's optional-dependent structural comparison contract.";

    [ConditionalTheory(Skip = MySqlOptionalDependentGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Filter_nested_optional_dependent_with_all_optional_compared_to_null(bool async) => base.Filter_nested_optional_dependent_with_all_optional_compared_to_null(async);
    [ConditionalTheory(Skip = MySqlOptionalDependentGap)] [MemberData(nameof(IsAsyncData))]
    public override Task Filter_nested_optional_dependent_with_all_optional_compared_to_not_null(bool async) => base.Filter_nested_optional_dependent_with_all_optional_compared_to_not_null(async);
}

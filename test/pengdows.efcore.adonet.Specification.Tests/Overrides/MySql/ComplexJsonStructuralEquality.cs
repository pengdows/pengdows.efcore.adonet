namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class ComplexJsonStructuralEqualityMySqlTest
{
    private const string MySqlJsonStructuralGap = "[SKIP:MySQL] MySQL JSON comparison uses database JSON semantics rather than EF's structural CLR comparison contract for nested complex values and collections.";

    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Contains_with_operators_composed_on_the_collection() => base.Contains_with_operators_composed_on_the_collection();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Contains_with_inline() => base.Contains_with_inline();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Contains_with_nested_and_composed_operators() => base.Contains_with_nested_and_composed_operators();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Contains_with_parameter() => base.Contains_with_parameter();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_associate_with_inline() => base.Nested_associate_with_inline();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_associate_with_inline_null() => base.Nested_associate_with_inline_null();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_associate_with_parameter() => base.Nested_associate_with_parameter();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_collection_with_inline() => base.Nested_collection_with_inline();
    [ConditionalFact(Skip = MySqlJsonStructuralGap)] public override Task Nested_collection_with_parameter() => base.Nested_collection_with_parameter();
}

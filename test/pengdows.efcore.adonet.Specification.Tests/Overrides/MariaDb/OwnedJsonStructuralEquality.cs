namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class OwnedJsonStructuralEqualityMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL JSON comparison does not implement EF's structural CLR comparison contract for nested owned JSON values.")]
    public override Task Nested_associate_with_inline_null() => base.Nested_associate_with_inline_null();
}

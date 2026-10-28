namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class ComplexJsonBulkUpdateMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] EF does not translate ExecuteUpdate assignments to an indexed element of a JSON primitive collection; MySQL JSON storage cannot represent this provider-neutral update shape.")]
    public override Task Update_inside_primitive_collection() => base.Update_inside_primitive_collection();
}

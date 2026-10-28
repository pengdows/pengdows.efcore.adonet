namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class RelationalComplianceMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] The generated provider-neutral compliance inventory includes EF test bases that are intentionally not implemented for MySQL capabilities.")]
    public override void All_test_bases_must_be_implemented() => base.All_test_bases_must_be_implemented();
}

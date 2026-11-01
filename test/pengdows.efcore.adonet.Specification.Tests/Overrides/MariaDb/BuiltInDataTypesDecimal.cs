namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class BuiltInDataTypesMariaDbTest
{
    // A decimal reads back from DECIMAL(65,30) with that storage's 30 places, so its string form has more digits than EF's
    // expectation (the value is equal). MariaDB shares MySQL's decimal storage (MySqlNumericFidelityTests pins it).
    [ConditionalFact(Skip = "[SKIP:MariaDB] MariaDB stores decimal as DECIMAL(65,30) so every CLR decimal round-trips exactly; EF's expected string form reflects SQL Server's decimal(18,2)-style scale. The value is equal.")]
    public override Task Object_to_string_conversion() => base.Object_to_string_conversion();
}

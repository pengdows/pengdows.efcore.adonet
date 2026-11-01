using pengdows.efcore.adonet;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Covers and CoveredBy are the OGC/SQL-MM predicates: the ISO template table names them, so a dialect that has them
///     inherits them, and a dialect that does not declares them null, leaving them untranslated rather than wrong.
/// </summary>
public class SpatialCoversSyntaxTests
{
    [Fact]
    public void Covers_is_ST_Covers_in_the_standard_table()
        => Assert.Equal("ST_Covers({0}, {1})", new SpatialSyntax().Function("Covers"));

    [Fact]
    public void CoveredBy_is_ST_CoveredBy_in_the_standard_table()
        => Assert.Equal("ST_CoveredBy({0}, {1})", new SpatialSyntax().Function("CoveredBy"));

    [Fact]
    public void A_dialect_without_Covers_declares_it_null_and_is_not_given_the_standard_template()
        => Assert.Null(new SpatialSyntax { Functions = new Dictionary<string, string?> { ["Covers"] = null } }.Function("Covers"));
}

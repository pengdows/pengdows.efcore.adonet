global using static pengdows.efcore.adonet.mysql.Tests.TestQuoting;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     The identifier quote character these tests expect in generated SQL, read from the dialect itself: a test asserting a
///     literal backtick (or any other character) directly would need another mechanical sweep the next time the dialect's
///     quoting changes, the way it just did.
/// </summary>
internal static class TestQuoting
{
    // MySQL's prefix and suffix are the same character, so one constant covers both ends of an identifier.
    public static readonly string Q = MySqlDialect.Instance.Sql.IdentifierQuotePrefix;
}

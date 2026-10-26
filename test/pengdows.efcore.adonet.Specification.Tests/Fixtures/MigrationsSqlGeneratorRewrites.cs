namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class MigrationsSqlGeneratorAdoNetTest<THost>
{
    // The guard asks each provider to override every test with its own SQL baseline. The base suite checks results, not a
    // provider's SQL (README.md), so the generated SQL is checked by running it, in the migrations suites.
    [ConditionalFact(Skip = "[SKIP:all] Results only: no per-provider SQL baselines")]
    public new void All_tests_must_be_overriden()
        => base.All_tests_must_be_overriden();
}

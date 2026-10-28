namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class MigrationsInfrastructureMariaDbTest
{
    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL cannot generate EF's provider-independent idempotent migration script without conditional SQL blocks")]
    public override Task Can_generate_idempotent_up_and_down_scripts()
        => base.Can_generate_idempotent_up_and_down_scripts();

    [ConditionalFact(Skip = "[SKIP:MariaDB] MySQL cannot generate EF's provider-independent idempotent migration script without conditional SQL blocks")]
    public override Task Can_generate_idempotent_up_and_down_scripts_noTransactions()
        => base.Can_generate_idempotent_up_and_down_scripts_noTransactions();

    [ConditionalFact(Skip = "[SKIP:MariaDB] Legacy EF migration snapshots are not part of this provider-neutral suite")]
    public override void Can_diff_against_2_2_model()
        => base.Can_diff_against_2_2_model();

    [ConditionalFact(Skip = "[SKIP:MariaDB] Legacy EF migration snapshots are not part of this provider-neutral suite")]
    public override void Can_diff_against_3_0_ASP_NET_Identity_model()
        => base.Can_diff_against_3_0_ASP_NET_Identity_model();

    [ConditionalFact(Skip = "[SKIP:MariaDB] Legacy EF migration snapshots are not part of this provider-neutral suite")]
    public override void Can_diff_against_2_2_ASP_NET_Identity_model()
        => base.Can_diff_against_2_2_ASP_NET_Identity_model();

    [ConditionalFact(Skip = "[SKIP:MariaDB] Legacy EF migration snapshots are not part of this provider-neutral suite")]
    public override void Can_diff_against_2_1_ASP_NET_Identity_model()
        => base.Can_diff_against_2_1_ASP_NET_Identity_model();
}

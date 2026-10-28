namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class StringTranslationsMariaDbTest
{
    private const string MySqlStringTranslationGap = "[SKIP:MariaDB] MySQL's string functions do not provide the EF/Pomelo translation contract for this operation.";

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task FirstOrDefault() => base.FirstOrDefault();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task LastOrDefault() => base.LastOrDefault();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task Join_non_aggregate() => base.Join_non_aggregate();
}

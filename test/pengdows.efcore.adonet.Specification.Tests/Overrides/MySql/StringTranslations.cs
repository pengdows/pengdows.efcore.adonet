namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class StringTranslationsMySqlTest
{
    private const string MySqlStringTranslationGap = "[SKIP:MySQL] MySQL's string functions do not provide the EF/Pomelo translation contract for this operation.";

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task TrimEnd_with_char_array_argument() => base.TrimEnd_with_char_array_argument();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task Trim_with_char_array_argument_in_predicate() => base.Trim_with_char_array_argument_in_predicate();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task TrimStart_with_char_array_argument() => base.TrimStart_with_char_array_argument();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task FirstOrDefault() => base.FirstOrDefault();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task LastOrDefault() => base.LastOrDefault();

    [ConditionalFact(Skip = MySqlStringTranslationGap)]
    public override Task Join_non_aggregate() => base.Join_non_aggregate();
}

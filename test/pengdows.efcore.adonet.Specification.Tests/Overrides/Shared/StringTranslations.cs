namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class StringTranslationsAdoNetTest<THost>
{
    // String.Trim(char[]) of several characters needs a trim of a set of characters, which ISO's TRIM (one character) and
    // MySQL's (one string) aren't: skipped where the dialect has none (FunctionSyntax.TrimCharacters).
    private static void RequireCharacterSetTrim()
    {
        var dialect = new THost().Dialect;
        if (dialect.Sql.Functions.TrimCharacters == null)
        {
            throw new CapabilitySkipException($"[SKIP:{dialect.Name}] {dialect.Name} has no trim of a set of characters.");
        }
    }

    public override Task TrimStart_with_char_array_argument()
    {
        RequireCharacterSetTrim();
        return base.TrimStart_with_char_array_argument();
    }

    public override Task TrimEnd_with_char_array_argument()
    {
        RequireCharacterSetTrim();
        return base.TrimEnd_with_char_array_argument();
    }

    public override Task Trim_with_char_array_argument_in_predicate()
    {
        RequireCharacterSetTrim();
        return base.Trim_with_char_array_argument_in_predicate();
    }
}

namespace pengdows.efcore.adonet;

/// <summary>
///     How the database spells the functions the core translates .NET members to. Each is a template: <c>{0}</c>,
///     <c>{1}</c>... are the arguments, <c>{{</c> and <c>}}</c> are literal braces. ISO SQL by default; null when the
///     database has no equivalent, which leaves that member untranslated.
/// </summary>
/// <remarks>
///     Positions are 1-based: the core converts .NET's 0-based indexes before filling a template.
/// </remarks>
public class FunctionSyntax
{
    public static readonly FunctionSyntax Iso = new();

    /// <summary>String concatenation: the operator for <c>a + b</c> on strings.</summary>
    public virtual string Concat => "{0} || {1}";

    public virtual string? Upper => "UPPER({0})";
    public virtual string? Lower => "LOWER({0})";
    public virtual string? Trim => "TRIM({0})";
    public virtual string? TrimStart => "TRIM(LEADING FROM {0})";
    public virtual string? TrimEnd => "TRIM(TRAILING FROM {0})";

    /// <summary>Length in characters.</summary>
    public virtual string? Length => "CHAR_LENGTH({0})";

    /// <summary><c>{0}</c> from 1-based position <c>{1}</c> for <c>{2}</c> characters.</summary>
    public virtual string? Substring => "SUBSTRING({0} FROM {1} FOR {2})";

    /// <summary><c>{0}</c> from 1-based position <c>{1}</c> to the end.</summary>
    public virtual string? SubstringFrom => "SUBSTRING({0} FROM {1})";

    /// <summary>The 1-based position of <c>{0}</c> in <c>{1}</c>; 0 when absent, 1 when <c>{0}</c> is empty.</summary>
    public virtual string? Position => "POSITION({0} IN {1})";

    public virtual string? Replace => "REPLACE({0}, {1}, {2})";

    public virtual string? Abs => "ABS({0})";
    public virtual string? Floor => "FLOOR({0})";
    public virtual string? Ceiling => "CEILING({0})";
    public virtual string? Round => "ROUND({0})";

    /// <summary><c>{0}</c> rounded to <c>{1}</c> decimal places.</summary>
    public virtual string? RoundDigits => "ROUND({0}, {1})";

    public virtual string? Power => "POWER({0}, {1})";

    /// <summary>
    ///     The remainder of <c>{0}</c> divided by <c>{1}</c>: ISO's <c>MOD</c>, which has no operator for it. Null keeps
    ///     EF's <c>%</c> operator.
    /// </summary>
    public virtual string? Modulo => "MOD({0}, {1})";
    public virtual string? Sqrt => "SQRT({0})";

    public virtual string? Year => "EXTRACT(YEAR FROM {0})";
    public virtual string? Month => "EXTRACT(MONTH FROM {0})";
    public virtual string? Day => "EXTRACT(DAY FROM {0})";
    public virtual string? Hour => "EXTRACT(HOUR FROM {0})";
    public virtual string? Minute => "EXTRACT(MINUTE FROM {0})";

    /// <summary>Whole seconds. ISO's SECOND field keeps the fraction (8.5), so the default floors it.</summary>
    public virtual string? Second => "CAST(FLOOR(EXTRACT(SECOND FROM {0})) AS INTEGER)";

    /// <summary>
    ///     A <see cref="DateTimeOffset" /> as written before a date part is taken from it, so the parts are those of the offset
    ///     the value reads back with, as .NET's are; it fills <c>{0}</c> of <see cref="Year" />...<see cref="Second" />. ISO's
    ///     parts of a TIMESTAMP WITH TIME ZONE are already the value's own. Null leaves the parts untranslated.
    /// </summary>
    public virtual string? OffsetDateTime => "{0}";

    /// <summary>
    ///     Escapes LIKE wildcards in patterns from constants and parameters; also written as the pattern's <c>ESCAPE</c>
    ///     character. Null for a LIKE without ESCAPE: patterns that may hold wildcards are then compared by position.
    /// </summary>
    public virtual char? LikeEscapeCharacter => '\\';

    /// <summary>A LIKE pattern matching <paramref name="value" /> literally, or null when it needs escaping and can't be.</summary>
    public string? EscapeLikePattern(string value)
    {
        if (!value.Any(c => (c == LikeEscapeCharacter) || LikeSpecialCharacters.Contains(c)))
        {
            return value;
        }

        if (LikeEscapeCharacter is not { } escape)
        {
            return null;
        }

        var pattern = new System.Text.StringBuilder(value.Length * 2);
        foreach (var c in value)
        {
            if ((c == escape) || LikeSpecialCharacters.Contains(c))
            {
                pattern.Append(escape);
            }

            pattern.Append(c);
        }

        return pattern.ToString();
    }

    /// <summary>Characters LIKE treats specially, besides the escape character.</summary>
    public virtual string LikeSpecialCharacters => "%_";

    /// <summary>Each template, by member name, with the number of arguments it is filled with.</summary>
    internal IEnumerable<(string Member, string? Template, int Arguments)> Templates()
        =>
        [
            (nameof(Concat), Concat, 2), (nameof(Upper), Upper, 1), (nameof(Lower), Lower, 1), (nameof(Trim), Trim, 1),
            (nameof(TrimStart), TrimStart, 1), (nameof(TrimEnd), TrimEnd, 1), (nameof(Length), Length, 1),
            (nameof(Substring), Substring, 3), (nameof(SubstringFrom), SubstringFrom, 2), (nameof(Position), Position, 2),
            (nameof(Replace), Replace, 3), (nameof(Abs), Abs, 1), (nameof(Floor), Floor, 1), (nameof(Ceiling), Ceiling, 1),
            (nameof(Round), Round, 1), (nameof(RoundDigits), RoundDigits, 2), (nameof(Power), Power, 2), (nameof(Modulo), Modulo, 2), (nameof(Sqrt), Sqrt, 1),
            (nameof(Year), Year, 1), (nameof(Month), Month, 1), (nameof(Day), Day, 1), (nameof(Hour), Hour, 1),
            (nameof(Minute), Minute, 1), (nameof(Second), Second, 1), (nameof(OffsetDateTime), OffsetDateTime, 1)
        ];
}

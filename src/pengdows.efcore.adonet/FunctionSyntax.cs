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

    /// <summary>Escapes LIKE wildcards in constant patterns; also written as the pattern's <c>ESCAPE</c> character.</summary>
    public virtual char LikeEscapeCharacter => '\\';

    /// <summary>Characters LIKE treats specially, besides the escape character.</summary>
    public virtual string LikeSpecialCharacters => "%_";
}

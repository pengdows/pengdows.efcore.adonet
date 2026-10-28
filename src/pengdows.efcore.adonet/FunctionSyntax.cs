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

    /// <summary>
    ///     Builds the runtime value for a parameterized <c>LIKE</c> operation. The arguments are deliberately primitive so
    ///     EF's precompiled-query generator can emit the call without capturing a provider-specific syntax object.
    /// </summary>
    public static string? FormatLikePattern(
        Microsoft.EntityFrameworkCore.Query.QueryContext queryContext,
        string parameterName,
        string match,
        char escape,
        string specialCharacters)
    {
        if (queryContext.Parameters[parameterName] is not string value)
        {
            return null;
        }

        var pattern = EscapeLikePattern(value, escape, specialCharacters);
        return match switch
        {
            nameof(string.StartsWith) => pattern + "%",
            nameof(string.EndsWith) => "%" + pattern,
            nameof(string.Contains) => "%" + pattern + "%",
            _ => throw new ArgumentOutOfRangeException(nameof(match), match, "A LIKE pattern is built for StartsWith, EndsWith or Contains.")
        };
    }

    private static string EscapeLikePattern(string value, char escape, string specialCharacters)
    {
        if (!value.Any(c => (c == escape) || specialCharacters.Contains(c)))
        {
            return value;
        }

        var pattern = new System.Text.StringBuilder(value.Length * 2);
        foreach (var c in value)
        {
            if ((c == escape) || specialCharacters.Contains(c))
            {
                pattern.Append(escape);
            }

            pattern.Append(c);
        }

        return pattern.ToString();
    }

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

    /// <summary>
    ///     Integer <c>{0} &amp; {1}</c>. ISO SQL has no bitwise operators: null, the ISO default, leaves them untranslated,
    ///     as for every member here; a database with them names its own.
    /// </summary>
    public virtual string? BitwiseAnd => null;

    /// <summary>Integer <c>{0} | {1}</c>; null where the database has no such operator.</summary>
    public virtual string? BitwiseOr => null;

    /// <summary>Integer <c>~{0}</c>; null where the database has no such operator.</summary>
    public virtual string? BitwiseNot => null;

    /// <summary>
    ///     Integer <c>{0} ^ {1}</c>; null where the database has no such operator. A database with <c>|</c> and <c>&amp;</c>
    ///     but no exclusive or can write <c>(({0} | {1}) - ({0} &amp; {1}))</c>, which equals it for integers.
    /// </summary>
    public virtual string? ExclusiveOr => null;
    public virtual string? Sqrt => "SQRT({0})";

    // ISO SQL:2016's trigonometric and logarithmic functions (features T622-T624).
    public virtual string? Acos => "ACOS({0})";
    public virtual string? Asin => "ASIN({0})";
    public virtual string? Atan => "ATAN({0})";
    public virtual string? Cos => "COS({0})";
    public virtual string? Sin => "SIN({0})";
    public virtual string? Tan => "TAN({0})";
    public virtual string? Cosh => "COSH({0})";
    public virtual string? Sinh => "SINH({0})";
    public virtual string? Tanh => "TANH({0})";
    public virtual string? Exp => "EXP({0})";

    /// <summary>The natural logarithm.</summary>
    public virtual string? Ln => "LN({0})";

    public virtual string? Log10 => "LOG10({0})";

    /// <summary>The logarithm of <c>{0}</c> in base <c>{1}</c>.</summary>
    public virtual string? LogBase => "LOG({1}, {0})";

    /// <summary>The angle from the x-axis to the point (<c>{1}</c>, <c>{0}</c>): .NET's <c>Atan2(y, x)</c>.</summary>
    public virtual string? Atan2 => null;

    public virtual string? Acosh => null;
    public virtual string? Asinh => null;
    public virtual string? Atanh => null;
    public virtual string? Log2 => null;

    /// <summary>-1, 0 or 1, as an integer.</summary>
    public virtual string? Sign => null;

    /// <summary><c>{0}</c> with its fraction dropped, toward zero.</summary>
    public virtual string? Truncate => null;

    /// <summary><c>{0}</c> radians in degrees.</summary>
    public virtual string? Degrees => null;

    /// <summary><c>{0}</c> degrees in radians.</summary>
    public virtual string? Radians => null;

    /// <summary>The larger of <c>{0}</c> and <c>{1}</c>.</summary>
    public virtual string? Greatest => null;

    /// <summary>The smaller of <c>{0}</c> and <c>{1}</c>.</summary>
    public virtual string? Least => null;

    /// <summary>Integer <c>{0} &lt;&lt; {1}</c>; null where the database has no such operator.</summary>
    public virtual string? LeftShift => null;

    /// <summary>Integer <c>{0} &gt;&gt; {1}</c>; null where the database has no such operator.</summary>
    public virtual string? RightShift => null;

    /// <summary>The 1-based position of <c>{0}</c> in <c>{1}</c> from 1-based position <c>{2}</c>; 0 when absent.</summary>
    public virtual string? PositionFrom => null;

    /// <summary><c>{0}</c> with the single character <c>{1}</c> removed from both ends.</summary>
    public virtual string? TrimCharacter => "TRIM(BOTH {1} FROM {0})";

    /// <summary><c>{0}</c> with the single character <c>{1}</c> removed from its start.</summary>
    public virtual string? TrimStartCharacter => "TRIM(LEADING {1} FROM {0})";

    /// <summary><c>{0}</c> with the single character <c>{1}</c> removed from its end.</summary>
    public virtual string? TrimEndCharacter => "TRIM(TRAILING {1} FROM {0})";

    /// <summary>
    ///     <c>{0}</c> padded on the left to <c>{1}</c> characters with the one-character string <c>{2}</c>, and kept whole when
    ///     already as long (<c>String.PadLeft</c>; SQL's LPAD would cut it); null where the database has no padding (ISO).
    /// </summary>
    public virtual string? PadLeft => null;

    /// <summary><c>{0}</c> padded on the right to <c>{1}</c> characters with <c>{2}</c>, kept whole when already as long.</summary>
    public virtual string? PadRight => null;

    /// <summary>
    ///     <c>{0}</c> with every character in the string <c>{1}</c> removed from both ends, in any order
    ///     (<c>String.Trim(char[])</c>); null where the database's trim takes one character, as ISO's does.
    /// </summary>
    public virtual string? TrimCharacters => null;

    /// <summary><c>{0}</c> with every character in the string <c>{1}</c> removed from its start; null where there is none.</summary>
    public virtual string? TrimStartCharacters => null;

    /// <summary><c>{0}</c> with every character in the string <c>{1}</c> removed from its end; null where there is none.</summary>
    public virtual string? TrimEndCharacters => null;

    /// <summary>Whether <c>{0}</c> matches the regular expression <c>{1}</c>, case-sensitively as .NET's.</summary>
    public virtual string? RegexIsMatch => null;

    /// <summary>A new random GUID.</summary>
    public virtual string? NewGuid => null;

    /// <summary>The day of the year, from 1.</summary>
    public virtual string? DayOfYear => null;

    /// <summary>The day of the week, from 0 for Sunday, as .NET's <see cref="System.DayOfWeek" />.</summary>
    public virtual string? DayOfWeek => null;

    /// <summary>The milliseconds of the second, 0 to 999.</summary>
    public virtual string? Millisecond => null;

    /// <summary>The microseconds of the millisecond, 0 to 999.</summary>
    public virtual string? Microsecond => null;

    /// <summary>The nanoseconds of the microsecond, 0 to 999: 0 where the database keeps microseconds; null for null.</summary>
    public virtual string? Nanosecond => null;

    /// <summary>The date at midnight, of the same type.</summary>
    public virtual string? DateOf => null;

    /// <summary>The time of day, as a time span.</summary>
    public virtual string? TimeOfDay => null;

    /// <summary>The current local date and time.</summary>
    public virtual string? Now => null;

    /// <summary>
    ///     The current date and time in UTC. ISO's CURRENT_TIMESTAMP is the session's time zone; its time zone clause gives the
    ///     same instant at a displacement of zero (9075-2 6.35).
    /// </summary>
    public virtual string? UtcNow => "CAST(CURRENT_TIMESTAMP AT TIME ZONE INTERVAL '00:00' HOUR TO MINUTE AS TIMESTAMP)";

    /// <summary>The current local date.</summary>
    public virtual string? Today => null;

    /// <summary><c>{0}</c> plus <c>{1}</c> whole years.</summary>
    public virtual string? AddYears => null;

    /// <summary><c>{0}</c> plus <c>{1}</c> whole months.</summary>
    public virtual string? AddMonths => null;

    /// <summary><c>{0}</c> plus <c>{1}</c> whole days.</summary>
    public virtual string? AddDays => null;

    /// <summary>
    ///     <c>{0}</c> plus <c>{1}</c> microseconds, rounded to whole ones. Hours, minutes, seconds and fractional days are
    ///     added through it, so a fraction of any is kept.
    /// </summary>
    public virtual string? AddMicroseconds => null;

    /// <summary>The day number of a date: days since 0001-01-01, as <see cref="DateOnly.DayNumber" />.</summary>
    public virtual string? DayNumber => null;

    /// <summary>The date of the date and time <c>{0}</c>.</summary>
    public virtual string? DateFromDateTime => "CAST({0} AS DATE)";

    /// <summary>The time of day of the date and time <c>{0}</c>.</summary>
    public virtual string? TimeFromDateTime => null;

    /// <summary>The time of day the time span <c>{0}</c> is.</summary>
    public virtual string? TimeFromTimeSpan => null;

    /// <summary>The date <c>{0}</c> at the time of day <c>{1}</c>.</summary>
    public virtual string? DateAtTime => null;

    /// <summary>The time of day <c>{0}</c> plus the time span <c>{1}</c>.</summary>
    public virtual string? AddTime => null;

    /// <summary>
    ///     Whether the time of day <c>{0}</c> is at or after <c>{1}</c> and before <c>{2}</c>, wrapping past midnight when
    ///     <c>{2}</c> is earlier, as <see cref="TimeOnly.IsBetween" />.
    /// </summary>
    public virtual string? TimeIsBetween => null;

    /// <summary>Time of day <c>{0}</c> minus <c>{1}</c>, wrapping past midnight as .NET's <c>TimeOnly - TimeOnly</c>.</summary>
    public virtual string? TimeDifference => null;

    /// <summary>The current instant, as a date and time with an offset.</summary>
    public virtual string? OffsetNow => null;

    /// <summary>Whole seconds since 1970-01-01T00:00:00Z, as <see cref="DateTimeOffset.ToUnixTimeSeconds" />.</summary>
    public virtual string? UnixSeconds => null;

    /// <summary>Whole milliseconds since 1970-01-01T00:00:00Z, as <see cref="DateTimeOffset.ToUnixTimeMilliseconds" />.</summary>
    public virtual string? UnixMilliseconds => null;

    /// <summary>The length of binary <c>{0}</c> in bytes.</summary>
    public virtual string? ByteLength => "OCTET_LENGTH({0})";

    /// <summary>The byte of binary <c>{0}</c> at 1-based position <c>{1}</c>, as an integer.</summary>
    public virtual string? ByteAt => null;

    /// <summary>Whether binary <c>{0}</c> contains the byte <c>{1}</c>.</summary>
    public virtual string? BytesContain => null;

    // The parts of a time span: whole hours of the day (not the total), minutes of the hour, and so on.
    public virtual string? TimeSpanHours => null;
    /// <summary>How a date/time difference is represented when a database has no interval type.</summary>
    public virtual string? DateDifference => null;
    /// <summary>How a date difference is converted directly to total days.</summary>
    public virtual string? DateDifferenceTotalDays => null;
    /// <summary>How <see cref="TimeSpan.TotalDays"/> is read from a translated span.</summary>
    public virtual string? TimeSpanTotalDays => null;
    public virtual string? TimeSpanAdd => null;
    public virtual string? TimeSpanSubtract => null;
    public virtual string? TimeSpanMinutes => null;
    public virtual string? TimeSpanSeconds => null;
    public virtual string? TimeSpanMilliseconds => null;
    public virtual string? TimeSpanMicroseconds => null;
    public virtual string? TimeSpanNanoseconds => null;

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
    public virtual string? EscapeLikePattern(string value)
        => LikeEscapeCharacter is { } escape
            ? EscapeLikePattern(value, escape, LikeSpecialCharacters)
            : (value.Any(c => LikeSpecialCharacters.Contains(c)) ? null : value);

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
            (nameof(Round), Round, 1), (nameof(RoundDigits), RoundDigits, 2), (nameof(Power), Power, 2),
            (nameof(Modulo), Modulo, 2), (nameof(BitwiseAnd), BitwiseAnd, 2), (nameof(BitwiseOr), BitwiseOr, 2),
            (nameof(BitwiseNot), BitwiseNot, 1), (nameof(ExclusiveOr), ExclusiveOr, 2), (nameof(Sqrt), Sqrt, 1),
            (nameof(Year), Year, 1), (nameof(Month), Month, 1), (nameof(Day), Day, 1), (nameof(Hour), Hour, 1),
            (nameof(Minute), Minute, 1), (nameof(Second), Second, 1), (nameof(OffsetDateTime), OffsetDateTime, 1),
            (nameof(Acos), Acos, 1), (nameof(Asin), Asin, 1), (nameof(Atan), Atan, 1), (nameof(Cos), Cos, 1),
            (nameof(Sin), Sin, 1), (nameof(Tan), Tan, 1), (nameof(Cosh), Cosh, 1), (nameof(Sinh), Sinh, 1),
            (nameof(Tanh), Tanh, 1), (nameof(Exp), Exp, 1), (nameof(Ln), Ln, 1), (nameof(Log10), Log10, 1),
            (nameof(LogBase), LogBase, 2), (nameof(Atan2), Atan2, 2), (nameof(Acosh), Acosh, 1), (nameof(Asinh), Asinh, 1),
            (nameof(Atanh), Atanh, 1), (nameof(Log2), Log2, 1), (nameof(Sign), Sign, 1), (nameof(Truncate), Truncate, 1),
            (nameof(Degrees), Degrees, 1), (nameof(Radians), Radians, 1), (nameof(Greatest), Greatest, 2),
            (nameof(Least), Least, 2), (nameof(LeftShift), LeftShift, 2), (nameof(RightShift), RightShift, 2),
            (nameof(PositionFrom), PositionFrom, 3), (nameof(TrimCharacter), TrimCharacter, 2),
            (nameof(TrimStartCharacter), TrimStartCharacter, 2), (nameof(TrimEndCharacter), TrimEndCharacter, 2),
            (nameof(TrimCharacters), TrimCharacters, 2), (nameof(TrimStartCharacters), TrimStartCharacters, 2),
            (nameof(TrimEndCharacters), TrimEndCharacters, 2), (nameof(PadLeft), PadLeft, 3), (nameof(PadRight), PadRight, 3),
            (nameof(RegexIsMatch), RegexIsMatch, 2), (nameof(NewGuid), NewGuid, 0), (nameof(DayOfYear), DayOfYear, 1),
            (nameof(DayOfWeek), DayOfWeek, 1), (nameof(Millisecond), Millisecond, 1), (nameof(Microsecond), Microsecond, 1),
            (nameof(Nanosecond), Nanosecond, 1), (nameof(DateOf), DateOf, 1), (nameof(TimeOfDay), TimeOfDay, 1),
            (nameof(Now), Now, 0), (nameof(UtcNow), UtcNow, 0), (nameof(Today), Today, 0), (nameof(AddYears), AddYears, 2),
            (nameof(AddMonths), AddMonths, 2), (nameof(AddDays), AddDays, 2), (nameof(AddMicroseconds), AddMicroseconds, 2),
            (nameof(DayNumber), DayNumber, 1), (nameof(DateFromDateTime), DateFromDateTime, 1),
            (nameof(TimeFromDateTime), TimeFromDateTime, 1), (nameof(TimeFromTimeSpan), TimeFromTimeSpan, 1),
            (nameof(DateAtTime), DateAtTime, 2), (nameof(AddTime), AddTime, 2), (nameof(TimeIsBetween), TimeIsBetween, 3),
            (nameof(TimeSpanHours), TimeSpanHours, 1), (nameof(TimeSpanMinutes), TimeSpanMinutes, 1),
            (nameof(TimeSpanSeconds), TimeSpanSeconds, 1), (nameof(TimeSpanMilliseconds), TimeSpanMilliseconds, 1),
            (nameof(TimeSpanMicroseconds), TimeSpanMicroseconds, 1), (nameof(TimeSpanNanoseconds), TimeSpanNanoseconds, 1),
            (nameof(TimeDifference), TimeDifference, 2), (nameof(OffsetNow), OffsetNow, 0),
            (nameof(UnixSeconds), UnixSeconds, 1), (nameof(UnixMilliseconds), UnixMilliseconds, 1),
            (nameof(ByteLength), ByteLength, 1), (nameof(ByteAt), ByteAt, 2), (nameof(BytesContain), BytesContain, 2)
        ];
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet;

/// <summary>
///     Database functions for LINQ queries, as <c>EF.Functions.X(...)</c>: each is translated to the dialect's SQL, and
///     throws when called outside a query.
/// </summary>
public static class AdoNetDbFunctionsExtensions
{
    /// <summary>The number of years from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffYear(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffYear)));

    /// <summary>The number of years from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffYear(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffYear)));

    /// <summary>The number of years from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffYear(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffYear)));

    /// <summary>The number of years from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffYear(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffYear)));

    /// <summary>The number of years from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffYear(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffYear)));

    /// <summary>The number of years from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffYear(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffYear)));

    /// <summary>The number of months from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMonth(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMonth)));

    /// <summary>The number of months from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMonth(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMonth)));

    /// <summary>The number of months from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMonth(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMonth)));

    /// <summary>The number of months from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMonth(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMonth)));

    /// <summary>The number of months from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMonth(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMonth)));

    /// <summary>The number of months from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMonth(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMonth)));

    /// <summary>The number of days from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffDay(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffDay)));

    /// <summary>The number of days from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffDay(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffDay)));

    /// <summary>The number of days from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffDay(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffDay)));

    /// <summary>The number of days from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffDay(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffDay)));

    /// <summary>The number of days from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffDay(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffDay)));

    /// <summary>The number of days from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffDay(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffDay)));

    /// <summary>The number of hours from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffHour(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of hours from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffHour(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of hours from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffHour(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of hours from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffHour(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of hours from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffHour(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of hours from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffHour(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of minutes from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMinute(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of minutes from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMinute(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of minutes from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMinute(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of minutes from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMinute(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of minutes from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMinute(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of minutes from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMinute(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of seconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffSecond(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of seconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffSecond(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of seconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffSecond(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of seconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffSecond(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of seconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffSecond(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of seconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffSecond(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of milliseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMillisecond(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of milliseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMillisecond(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of milliseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMillisecond(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of milliseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMillisecond(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of milliseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMillisecond(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of milliseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMillisecond(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of microseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMicrosecond(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of microseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMicrosecond(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of microseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMicrosecond(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of microseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMicrosecond(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of microseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int DateDiffMicrosecond(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of microseconds from <paramref name="start" /> to <paramref name="end" />, as the database counts them.</summary>
    public static int? DateDiffMicrosecond(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));
}

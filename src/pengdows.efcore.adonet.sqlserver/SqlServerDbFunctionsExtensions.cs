using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     SQL Server's database functions for LINQ queries, as <c>EF.Functions.X(...)</c>, with Microsoft's names and signatures:
///     full-text search, DATEDIFF in the units and over the types the core's <c>DateDiff*</c> lacks, the *FROMPARTS
///     constructors, ISDATE, ISNUMERIC, DATALENGTH, AT TIME ZONE, PATINDEX and the statistical aggregates. Each is translated
///     to T-SQL and throws when called outside a query. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerDbFunctionsExtensions.cs
/// </summary>
public static class SqlServerDbFunctionsExtensions
{
    /// <summary>Whether the full-text indexed column <paramref name="propertyReference" /> matches the meaning of <paramref name="freeText" />: FREETEXT.</summary>
    public static bool FreeText(this DbFunctions _, object propertyReference, string freeText)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FreeText)));

    /// <summary>Whether the full-text indexed column <paramref name="propertyReference" /> matches the meaning of <paramref name="freeText" /> in the language <paramref name="languageTerm" /> (an LCID from <c>sys.syslanguages</c>): FREETEXT.</summary>
    public static bool FreeText(this DbFunctions _, object propertyReference, string freeText, [NotParameterized] int languageTerm)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FreeText)));

    /// <summary>Whether the full-text indexed column <paramref name="propertyReference" /> meets <paramref name="searchCondition" />: CONTAINS.</summary>
    public static bool Contains(this DbFunctions _, object propertyReference, string searchCondition)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Contains)));

    /// <summary>Whether the full-text indexed column <paramref name="propertyReference" /> meets <paramref name="searchCondition" /> in the language <paramref name="languageTerm" /> (an LCID from <c>sys.syslanguages</c>): CONTAINS.</summary>
    public static bool Contains(this DbFunctions _, object propertyReference, string searchCondition, [NotParameterized] int languageTerm)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Contains)));

    /// <summary>The number of week boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(week, start, end).</summary>
    public static int DateDiffWeek(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffWeek)));

    /// <summary>The number of week boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(week, start, end).</summary>
    public static int? DateDiffWeek(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffWeek)));

    /// <summary>The number of week boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(week, start, end).</summary>
    public static int DateDiffWeek(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffWeek)));

    /// <summary>The number of week boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(week, start, end).</summary>
    public static int? DateDiffWeek(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffWeek)));

    /// <summary>The number of week boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(week, start, end).</summary>
    public static int DateDiffWeek(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffWeek)));

    /// <summary>The number of week boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(week, start, end).</summary>
    public static int? DateDiffWeek(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffWeek)));

    /// <summary>The number of hour boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(hour, start, end).</summary>
    public static int DateDiffHour(this DbFunctions _, TimeSpan start, TimeSpan end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of hour boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(hour, start, end).</summary>
    public static int? DateDiffHour(this DbFunctions _, TimeSpan? start, TimeSpan? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of hour boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(hour, start, end).</summary>
    public static int DateDiffHour(this DbFunctions _, TimeOnly start, TimeOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of hour boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(hour, start, end).</summary>
    public static int? DateDiffHour(this DbFunctions _, TimeOnly? start, TimeOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffHour)));

    /// <summary>The number of minute boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(minute, start, end).</summary>
    public static int DateDiffMinute(this DbFunctions _, TimeSpan start, TimeSpan end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of minute boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(minute, start, end).</summary>
    public static int? DateDiffMinute(this DbFunctions _, TimeSpan? start, TimeSpan? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of minute boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(minute, start, end).</summary>
    public static int DateDiffMinute(this DbFunctions _, TimeOnly start, TimeOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of minute boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(minute, start, end).</summary>
    public static int? DateDiffMinute(this DbFunctions _, TimeOnly? start, TimeOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMinute)));

    /// <summary>The number of second boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(second, start, end).</summary>
    public static int DateDiffSecond(this DbFunctions _, TimeSpan start, TimeSpan end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of second boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(second, start, end).</summary>
    public static int? DateDiffSecond(this DbFunctions _, TimeSpan? start, TimeSpan? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of second boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(second, start, end).</summary>
    public static int DateDiffSecond(this DbFunctions _, TimeOnly start, TimeOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of second boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(second, start, end).</summary>
    public static int? DateDiffSecond(this DbFunctions _, TimeOnly? start, TimeOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffSecond)));

    /// <summary>The number of millisecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(millisecond, start, end).</summary>
    public static int DateDiffMillisecond(this DbFunctions _, TimeSpan start, TimeSpan end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of millisecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(millisecond, start, end).</summary>
    public static int? DateDiffMillisecond(this DbFunctions _, TimeSpan? start, TimeSpan? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of millisecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(millisecond, start, end).</summary>
    public static int DateDiffMillisecond(this DbFunctions _, TimeOnly start, TimeOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of millisecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(millisecond, start, end).</summary>
    public static int? DateDiffMillisecond(this DbFunctions _, TimeOnly? start, TimeOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMillisecond)));

    /// <summary>The number of microsecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(microsecond, start, end).</summary>
    public static int DateDiffMicrosecond(this DbFunctions _, TimeSpan start, TimeSpan end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of microsecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(microsecond, start, end).</summary>
    public static int? DateDiffMicrosecond(this DbFunctions _, TimeSpan? start, TimeSpan? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of microsecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(microsecond, start, end).</summary>
    public static int DateDiffMicrosecond(this DbFunctions _, TimeOnly start, TimeOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of microsecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(microsecond, start, end).</summary>
    public static int? DateDiffMicrosecond(this DbFunctions _, TimeOnly? start, TimeOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffMicrosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int DateDiffNanosecond(this DbFunctions _, DateTime start, DateTime end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int? DateDiffNanosecond(this DbFunctions _, DateTime? start, DateTime? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int DateDiffNanosecond(this DbFunctions _, DateTimeOffset start, DateTimeOffset end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int? DateDiffNanosecond(this DbFunctions _, DateTimeOffset? start, DateTimeOffset? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int DateDiffNanosecond(this DbFunctions _, DateOnly start, DateOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int? DateDiffNanosecond(this DbFunctions _, DateOnly? start, DateOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int DateDiffNanosecond(this DbFunctions _, TimeSpan start, TimeSpan end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int? DateDiffNanosecond(this DbFunctions _, TimeSpan? start, TimeSpan? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int DateDiffNanosecond(this DbFunctions _, TimeOnly start, TimeOnly end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>The number of nanosecond boundaries crossed from <paramref name="start" /> to <paramref name="end" />: DATEDIFF(nanosecond, start, end).</summary>
    public static int? DateDiffNanosecond(this DbFunctions _, TimeOnly? start, TimeOnly? end)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateDiffNanosecond)));

    /// <summary>Whether <paramref name="expression" /> is a valid date, time or datetime: ISDATE.</summary>
    public static bool IsDate(this DbFunctions _, string expression)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsDate)));

    /// <summary>A <c>datetime</c> from its parts: DATETIMEFROMPARTS.</summary>
    public static DateTime DateTimeFromParts(this DbFunctions _, int year, int month, int day, int hour, int minute, int second, int millisecond)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateTimeFromParts)));

    /// <summary>A <c>date</c> from its parts: DATEFROMPARTS.</summary>
    public static DateTime DateFromParts(this DbFunctions _, int year, int month, int day)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateFromParts)));

    /// <summary>A <c>datetime2</c> from its parts, <paramref name="fractions" /> in units of 10^-<paramref name="precision" /> seconds: DATETIME2FROMPARTS.</summary>
    public static DateTime DateTime2FromParts(this DbFunctions _, int year, int month, int day, int hour, int minute, int second, int fractions, [NotParameterized] int precision)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateTime2FromParts)));

    /// <summary>A <c>datetimeoffset</c> from its parts, <paramref name="fractions" /> in units of 10^-<paramref name="precision" /> seconds: DATETIMEOFFSETFROMPARTS.</summary>
    public static DateTimeOffset DateTimeOffsetFromParts(this DbFunctions _, int year, int month, int day, int hour, int minute, int second, int fractions, int hourOffset, int minuteOffset, [NotParameterized] int precision)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DateTimeOffsetFromParts)));

    /// <summary>A <c>smalldatetime</c> from its parts: SMALLDATETIMEFROMPARTS.</summary>
    public static DateTime SmallDateTimeFromParts(this DbFunctions _, int year, int month, int day, int hour, int minute)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SmallDateTimeFromParts)));

    /// <summary>A <c>time</c> from its parts, <paramref name="fractions" /> in units of 10^-<paramref name="precision" /> seconds: TIMEFROMPARTS.</summary>
    public static TimeSpan TimeFromParts(this DbFunctions _, int hour, int minute, int second, int fractions, [NotParameterized] int precision)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TimeFromParts)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, string? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, bool? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, double? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, decimal? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, DateTime? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, TimeSpan? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, DateTimeOffset? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, byte[]? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>The number of bytes <paramref name="arg" /> takes: DATALENGTH.</summary>
    public static int? DataLength(this DbFunctions _, Guid? arg)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DataLength)));

    /// <summary>Whether <paramref name="expression" /> is a valid numeric type: ISNUMERIC.</summary>
    public static bool IsNumeric(this DbFunctions _, string expression)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsNumeric)));

    /// <summary><paramref name="dateTime" /> as a time in <paramref name="timeZone" /> (a Windows time zone name): AT TIME ZONE.</summary>
    public static DateTimeOffset AtTimeZone(this DbFunctions _, DateTime dateTime, string timeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(AtTimeZone)));

    /// <summary><paramref name="dateTimeOffset" /> converted to <paramref name="timeZone" /> (a Windows time zone name): AT TIME ZONE.</summary>
    public static DateTimeOffset AtTimeZone(this DbFunctions _, DateTimeOffset dateTimeOffset, string timeZone)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(AtTimeZone)));

    /// <summary>The 1-based position of the first match of the LIKE <paramref name="pattern" /> in <paramref name="expression" />, or 0: PATINDEX.</summary>
    public static long PatIndex(this DbFunctions _, string pattern, string expression)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(PatIndex)));

    /// <summary>The sample standard deviation of <paramref name="values" />: STDEV.</summary>
    public static double? StandardDeviationSample(this DbFunctions _, IEnumerable<byte> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationSample)));

    /// <summary>The sample standard deviation of <paramref name="values" />: STDEV.</summary>
    public static double? StandardDeviationSample(this DbFunctions _, IEnumerable<short> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationSample)));

    /// <summary>The sample standard deviation of <paramref name="values" />: STDEV.</summary>
    public static double? StandardDeviationSample(this DbFunctions _, IEnumerable<int> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationSample)));

    /// <summary>The sample standard deviation of <paramref name="values" />: STDEV.</summary>
    public static double? StandardDeviationSample(this DbFunctions _, IEnumerable<long> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationSample)));

    /// <summary>The sample standard deviation of <paramref name="values" />: STDEV.</summary>
    public static double? StandardDeviationSample(this DbFunctions _, IEnumerable<float> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationSample)));

    /// <summary>The sample standard deviation of <paramref name="values" />: STDEV.</summary>
    public static double? StandardDeviationSample(this DbFunctions _, IEnumerable<double> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationSample)));

    /// <summary>The sample standard deviation of <paramref name="values" />: STDEV.</summary>
    public static double? StandardDeviationSample(this DbFunctions _, IEnumerable<decimal> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationSample)));

    /// <summary>The population standard deviation of <paramref name="values" />: STDEVP.</summary>
    public static double? StandardDeviationPopulation(this DbFunctions _, IEnumerable<byte> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationPopulation)));

    /// <summary>The population standard deviation of <paramref name="values" />: STDEVP.</summary>
    public static double? StandardDeviationPopulation(this DbFunctions _, IEnumerable<short> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationPopulation)));

    /// <summary>The population standard deviation of <paramref name="values" />: STDEVP.</summary>
    public static double? StandardDeviationPopulation(this DbFunctions _, IEnumerable<int> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationPopulation)));

    /// <summary>The population standard deviation of <paramref name="values" />: STDEVP.</summary>
    public static double? StandardDeviationPopulation(this DbFunctions _, IEnumerable<long> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationPopulation)));

    /// <summary>The population standard deviation of <paramref name="values" />: STDEVP.</summary>
    public static double? StandardDeviationPopulation(this DbFunctions _, IEnumerable<float> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationPopulation)));

    /// <summary>The population standard deviation of <paramref name="values" />: STDEVP.</summary>
    public static double? StandardDeviationPopulation(this DbFunctions _, IEnumerable<double> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationPopulation)));

    /// <summary>The population standard deviation of <paramref name="values" />: STDEVP.</summary>
    public static double? StandardDeviationPopulation(this DbFunctions _, IEnumerable<decimal> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(StandardDeviationPopulation)));

    /// <summary>The sample variance of <paramref name="values" />: VAR.</summary>
    public static double? VarianceSample(this DbFunctions _, IEnumerable<byte> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VarianceSample)));

    /// <summary>The sample variance of <paramref name="values" />: VAR.</summary>
    public static double? VarianceSample(this DbFunctions _, IEnumerable<short> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VarianceSample)));

    /// <summary>The sample variance of <paramref name="values" />: VAR.</summary>
    public static double? VarianceSample(this DbFunctions _, IEnumerable<int> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VarianceSample)));

    /// <summary>The sample variance of <paramref name="values" />: VAR.</summary>
    public static double? VarianceSample(this DbFunctions _, IEnumerable<long> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VarianceSample)));

    /// <summary>The sample variance of <paramref name="values" />: VAR.</summary>
    public static double? VarianceSample(this DbFunctions _, IEnumerable<float> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VarianceSample)));

    /// <summary>The sample variance of <paramref name="values" />: VAR.</summary>
    public static double? VarianceSample(this DbFunctions _, IEnumerable<double> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VarianceSample)));

    /// <summary>The sample variance of <paramref name="values" />: VAR.</summary>
    public static double? VarianceSample(this DbFunctions _, IEnumerable<decimal> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VarianceSample)));

    /// <summary>The population variance of <paramref name="values" />: VARP.</summary>
    public static double? VariancePopulation(this DbFunctions _, IEnumerable<byte> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VariancePopulation)));

    /// <summary>The population variance of <paramref name="values" />: VARP.</summary>
    public static double? VariancePopulation(this DbFunctions _, IEnumerable<short> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VariancePopulation)));

    /// <summary>The population variance of <paramref name="values" />: VARP.</summary>
    public static double? VariancePopulation(this DbFunctions _, IEnumerable<int> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VariancePopulation)));

    /// <summary>The population variance of <paramref name="values" />: VARP.</summary>
    public static double? VariancePopulation(this DbFunctions _, IEnumerable<long> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VariancePopulation)));

    /// <summary>The population variance of <paramref name="values" />: VARP.</summary>
    public static double? VariancePopulation(this DbFunctions _, IEnumerable<float> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VariancePopulation)));

    /// <summary>The population variance of <paramref name="values" />: VARP.</summary>
    public static double? VariancePopulation(this DbFunctions _, IEnumerable<double> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VariancePopulation)));

    /// <summary>The population variance of <paramref name="values" />: VARP.</summary>
    public static double? VariancePopulation(this DbFunctions _, IEnumerable<decimal> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(VariancePopulation)));
}

using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     Covers Microsoft's SQL Server <c>EF.Functions</c> that the core has no shared form of: full-text FREETEXT and CONTAINS,
///     DATEDIFF in weeks and nanoseconds and over times, the *FROMPARTS constructors, ISDATE, ISNUMERIC, DATALENGTH, AT TIME
///     ZONE, PATINDEX and the statistical aggregates. SQL-shape tests; no server. Provider-only: these are T-SQL functions
///     (the shared DateDiff* units are in the core). Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerDbFunctionsExtensions.cs
/// </summary>
public class SqlServerDbFunctionsTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public int Group { get; set; }
        public double Score { get; set; }
        public int Count { get; set; }
        public DateTime At { get; set; }
        public DateTimeOffset AtOffset { get; set; }
        public DateOnly Day { get; set; }
        public TimeOnly Time { get; set; }
        public TimeSpan Span { get; set; }
        public byte[] Data { get; set; } = [];
    }

    private sealed class RowContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().Property(r => r.Code).HasMaxLength(50);
    }

    private static string Sql(Func<IQueryable<Row>, IQueryable> query, SqlServerDialect? dialect = null)
    {
        using var context = new RowContext(new DbContextOptionsBuilder<RowContext>()
            .UseAdoNet(Microsoft.Data.SqlClient.SqlClientFactory.Instance, "Server=.;Database=x", dialect ?? SqlServerDialect.Instance)
            .Options);
        return query(context.Rows).ToQueryString().ReplaceLineEndings("\n");
    }

    private static void Untranslated(Func<IQueryable<Row>, IQueryable> query, SqlServerDialect dialect)
        => Assert.Contains("could not be translated", Assert.Throws<InvalidOperationException>(() => Sql(query, dialect)).Message);

    [Fact]
    public void FreeText_is_a_bare_search_condition_in_where()
    {
        var sql = Sql(q => q.Where(r => EF.Functions.FreeText(r.Name, "bread")));

        Assert.Contains("WHERE FREETEXT([r].[Name], N'bread')", sql);
        Assert.DoesNotContain("CAST(1 AS bit)", sql);
    }

    [Fact]
    public void FreeText_and_Contains_take_a_language_term()
    {
        Assert.Contains("WHERE FREETEXT([r].[Name], N'bread', LANGUAGE 1033)", Sql(q => q.Where(r => EF.Functions.FreeText(r.Name, "bread", 1033))));
        Assert.Contains("WHERE CONTAINS([r].[Name], N'\"bre*\"', LANGUAGE 1036)", Sql(q => q.Where(r => EF.Functions.Contains(r.Name, "\"bre*\"", 1036))));
    }

    [Fact]
    public void Contains_is_negated_and_projected_as_a_condition()
    {
        Assert.Contains("WHERE NOT (CONTAINS([r].[Name], N'bread'))", Sql(q => q.Where(r => !EF.Functions.Contains(r.Name, "bread"))));
        Assert.Contains(
            "WHEN CONTAINS([r].[Name], N'bread') THEN CAST(1 AS bit)",
            Sql(q => q.Select(r => EF.Functions.Contains(r.Name, "bread"))));
    }

    /// <summary>Hostile search text goes in as a parameter, never into the SQL.</summary>
    [Fact]
    public void Full_text_search_text_from_a_variable_is_a_parameter()
    {
        var search = "x') OR 1=1; DROP TABLE [Rows]; --";

        var sql = Sql(q => q.Where(r => EF.Functions.FreeText(r.Name, search)));

        Assert.Contains("FREETEXT([r].[Name], @search)", sql);
        Assert.DoesNotContain("DROP TABLE", sql[sql.IndexOf("SELECT", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void Full_text_search_needs_a_column()
        => Assert.Contains(
            "column",
            Assert.Throws<InvalidOperationException>(() => Sql(q => q.Where(r => EF.Functions.FreeText(r.Name + "x", "bread")))).Message);

    [Fact]
    public void DateDiff_counts_weeks_and_nanoseconds_and_spans_of_time()
    {
        Assert.Contains("DATEDIFF(week, [r].[At], [r].[At])", Sql(q => q.Select(r => EF.Functions.DateDiffWeek(r.At, r.At))));
        Assert.Contains("DATEDIFF(week, [r].[Day], [r].[Day])", Sql(q => q.Select(r => EF.Functions.DateDiffWeek(r.Day, r.Day))));
        Assert.Contains("DATEDIFF(nanosecond, [r].[AtOffset], [r].[AtOffset])", Sql(q => q.Select(r => EF.Functions.DateDiffNanosecond(r.AtOffset, r.AtOffset))));
        Assert.Contains("DATEDIFF(nanosecond, [r].[Time], [r].[Time])", Sql(q => q.Select(r => EF.Functions.DateDiffNanosecond(r.Time, r.Time))));
        Assert.Contains("DATEDIFF(hour, [r].[Span], [r].[Span])", Sql(q => q.Select(r => EF.Functions.DateDiffHour(r.Span, r.Span))));
        Assert.Contains("DATEDIFF(microsecond, [r].[Time], [r].[Time])", Sql(q => q.Select(r => EF.Functions.DateDiffMicrosecond((TimeOnly?)r.Time, r.Time))));
    }

    [Fact]
    public void From_parts_functions_build_dates_and_times()
    {
        Assert.Contains("DATEFROMPARTS(2020, 2, 29)", Sql(q => q.Select(r => EF.Functions.DateFromParts(2020, 2, 29))));
        Assert.Contains("DATETIMEFROMPARTS(2020, 1, 2, 3, 4, 5, 6)", Sql(q => q.Select(r => EF.Functions.DateTimeFromParts(2020, 1, 2, 3, 4, 5, 6))));
        Assert.Contains("DATETIME2FROMPARTS(2020, 1, 2, 3, 4, 5, 6, 7)", Sql(q => q.Select(r => EF.Functions.DateTime2FromParts(2020, 1, 2, 3, 4, 5, 6, 7))));
        Assert.Contains(
            "DATETIMEOFFSETFROMPARTS(2020, 1, 2, 3, 4, 5, 6, -8, 0, 7)",
            Sql(q => q.Select(r => EF.Functions.DateTimeOffsetFromParts(2020, 1, 2, 3, 4, 5, 6, -8, 0, 7))));
        Assert.Contains("SMALLDATETIMEFROMPARTS(2020, 1, 2, 3, 4)", Sql(q => q.Select(r => EF.Functions.SmallDateTimeFromParts(2020, 1, 2, 3, 4))));
        Assert.Contains("TIMEFROMPARTS(23, 59, 59, 5, 1)", Sql(q => q.Select(r => EF.Functions.TimeFromParts(23, 59, 59, 5, 1))));
        Assert.Contains("DATEFROMPARTS([r].[Count], 1, 1)", Sql(q => q.Where(r => EF.Functions.DateFromParts(r.Count, 1, 1) > r.At)));
    }

    /// <summary>The *FROMPARTS functions came with SQL Server 2012 (compatibility level 110).</summary>
    [Fact]
    public void From_parts_functions_need_compatibility_level_110()
    {
        Untranslated(q => q.Where(r => EF.Functions.DateFromParts(2020, 1, 1) > r.At), SqlServerDialect.ForCompatibilityLevel(100));
        Assert.Contains("DATEFROMPARTS(2020, 1, 1)", Sql(q => q.Select(r => EF.Functions.DateFromParts(2020, 1, 1)), SqlServerDialect.ForCompatibilityLevel(110)));
    }

    [Fact]
    public void IsDate_and_IsNumeric_test_text()
    {
        Assert.Contains("WHERE CAST(ISDATE([r].[Name]) AS bit) = CAST(1 AS bit)", Sql(q => q.Where(r => EF.Functions.IsDate(r.Name))));
        Assert.Contains("WHERE ISNUMERIC([r].[Name]) = 1", Sql(q => q.Where(r => EF.Functions.IsNumeric(r.Name))));
    }

    /// <summary>DATALENGTH is bigint for a (max) type, so it is cast to the int the method returns.</summary>
    [Fact]
    public void DataLength_counts_bytes()
    {
        Assert.Contains("CAST(DATALENGTH([r].[Name]) AS int)", Sql(q => q.Select(r => EF.Functions.DataLength(r.Name))));
        Assert.Contains("CAST(DATALENGTH([r].[Data]) AS int)", Sql(q => q.Select(r => EF.Functions.DataLength(r.Data))));
        Assert.Contains("SELECT DATALENGTH([r].[Code])", Sql(q => q.Select(r => EF.Functions.DataLength(r.Code))));
        Assert.Contains("SELECT DATALENGTH([r].[Score])", Sql(q => q.Select(r => EF.Functions.DataLength(r.Score))));
        Assert.Contains("SELECT DATALENGTH([r].[At])", Sql(q => q.Select(r => EF.Functions.DataLength(r.At))));
    }

    [Fact]
    public void AtTimeZone_converts_to_a_datetimeoffset()
    {
        Assert.Contains("[r].[At] AT TIME ZONE N'Pacific Standard Time'", Sql(q => q.Select(r => EF.Functions.AtTimeZone(r.At, "Pacific Standard Time"))));
        Assert.Contains("[r].[AtOffset] AT TIME ZONE N'UTC'", Sql(q => q.Select(r => EF.Functions.AtTimeZone(r.AtOffset, "UTC"))));
        Assert.Contains("CAST('2020-01-02T03:04:05.0000000' AS datetime2) AT TIME ZONE N'UTC'", Sql(q => q.Where(r => EF.Functions.AtTimeZone(new DateTime(2020, 1, 2, 3, 4, 5), "UTC") > r.AtOffset)));
        Assert.Contains("END) AT TIME ZONE N'UTC'", Sql(q => q.Where(r => EF.Functions.AtTimeZone(r.Count > 0 ? r.At : r.At.Date, "UTC") > r.AtOffset)));
    }

    /// <summary>AT TIME ZONE came with SQL Server 2016 (compatibility level 130).</summary>
    [Fact]
    public void AtTimeZone_needs_compatibility_level_130()
        => Untranslated(q => q.Where(r => EF.Functions.AtTimeZone(r.At, "UTC") > r.AtOffset), SqlServerDialect.ForCompatibilityLevel(120));

    [Fact]
    public void PatIndex_finds_a_pattern()
        => Assert.Contains("PATINDEX(N'%[0-9]%', [r].[Name])", Sql(q => q.Select(r => EF.Functions.PatIndex("%[0-9]%", r.Name))));

    [Fact]
    public void Statistical_aggregates_are_stdev_and_var()
    {
        Assert.Contains("STDEV([r].[Score])", Sql(q => q.GroupBy(r => r.Group).Select(g => EF.Functions.StandardDeviationSample(g.Select(r => r.Score)))));
        Assert.Contains("STDEVP([r].[Count])", Sql(q => q.GroupBy(r => r.Group).Select(g => EF.Functions.StandardDeviationPopulation(g.Select(r => r.Count)))));
        Assert.Contains("VAR([r].[Score])", Sql(q => q.GroupBy(r => r.Group).Select(g => EF.Functions.VarianceSample(g.Select(r => r.Score)))));
        Assert.Contains(
            "VARP(DISTINCT ([r].[Count]))",
            Sql(q => q.GroupBy(r => r.Group).Select(g => EF.Functions.VariancePopulation(g.Select(r => r.Count).Distinct()))));
        Assert.Contains(
            "STDEV(CASE\n    WHEN [r].[Count] > 1 THEN [r].[Score]\nEND)",
            Sql(q => q.GroupBy(r => r.Group).Select(g => EF.Functions.StandardDeviationSample(g.Where(r => r.Count > 1).Select(r => r.Score)))));
    }

    [Fact]
    public void Functions_throw_outside_a_query()
        => Assert.Throws<InvalidOperationException>(() => EF.Functions.FreeText("a", "b"));
}

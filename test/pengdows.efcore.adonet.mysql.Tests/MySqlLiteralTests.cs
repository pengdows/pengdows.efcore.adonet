namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>TIME holds -838:59:59 to 838:59:59, as hours, minutes and seconds: days are written as hours.</summary>
public class MySqlLiteralTests
{
    public static TheoryData<TimeSpan, string> Literals => new()
    {
        { new TimeSpan(0, 1, 2, 3), "TIME '01:02:03'" },
        { new TimeSpan(0, 1, 2, 3, 500), "TIME '01:02:03.500000'" },
        { new TimeSpan(1, 2, 0, 0), "TIME '26:00:00'" },
        { -new TimeSpan(1, 1, 30, 0), "TIME '-25:30:00'" },
        { new TimeSpan(34, 22, 59, 59), "TIME '838:59:59'" }
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void Time_spans_are_written_as_hours(TimeSpan value, string literal)
        => Assert.Equal(literal, MySqlDialect.Instance.TypeMappings.FormatLiteral(value, "TIME(6)", "unused"));

    [Fact]
    public void Time_spans_outside_the_time_range_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MySqlDialect.Instance.TypeMappings.FormatLiteral(new TimeSpan(35, 0, 0, 0), "TIME(6)", "unused"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MySqlDialect.Instance.TypeMappings.FormatLiteral(-new TimeSpan(35, 0, 0, 0), "TIME(6)", "unused"));
    }
}

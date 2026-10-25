namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>Constants are written as the text or blob Microsoft.Data.Sqlite binds for the same value.</summary>
public class SqliteLiteralTests
{
    public static TheoryData<object, string> Literals => new()
    {
        { new DateTime(2024, 3, 5, 6, 7, 8), "'2024-03-05 06:07:08'" },
        { new DateTime(2024, 3, 5, 6, 7, 8, 123, 456), "'2024-03-05 06:07:08.123456'" },
        { new DateTimeOffset(2024, 3, 5, 1, 0, 0, 500, TimeSpan.FromHours(2)), "'2024-03-05 01:00:00.5+02:00'" },
        { new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.FromHours(-5.5)), "'2024-03-05 01:00:00-05:30'" },
        { new DateOnly(2024, 3, 5), "'2024-03-05'" },
        { new TimeOnly(6, 7, 8), "'06:07:08'" },
        { new TimeOnly(6, 7, 8, 500), "'06:07:08.5000000'" },
        { new TimeSpan(1, 2, 3, 4, 500), "'1.02:03:04.5000000'" },
        { -new TimeSpan(0, 1, 30, 0), "'-01:30:00'" },
        { new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"), "'0F8FAD5B-D9CB-469F-A165-70867728950E'" }
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void Writes_values_as_the_driver_binds_them(object value, string literal)
        => Assert.Equal(literal, SqliteDialect.Instance.TypeMappings.FormatLiteral(value, "", "unused"));
}

using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Provider parameter normalization for databases that store DateTimeOffset as a UTC instant.</summary>
public sealed class DateTimeOffsetParameterTests
{
    private sealed class UtcTypeMappings : AnsiTypeMappings
    {
        public override bool WritesDateTimeOffsetAsUtc => true;
    }

    private static readonly IAdoNetDialect UtcDialect = new PartsDialect(
        "UTC parameter mapping", typeMappings: new UtcTypeMappings());

    [Fact]
    public async Task Non_zero_offset_parameters_are_normalized_to_utc_without_changing_the_instant()
    {
        var database = new FakeDatabase();
        await using var context = database.Context(UtcDialect);
        var value = new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.FromHours(2));
        database.Returns(FakeDatabase.Row("Id", 1));

        context.Add(new Item { AtOffset = value });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var parameters = Assert.Single(database.Parameters);
        Assert.Contains("+00:00", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("+02:00", parameters, StringComparison.Ordinal);
        Assert.Contains(value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), parameters, StringComparison.Ordinal);
    }

    private static async Task<string> SavedParameters(IAdoNetDialect dialect, DateTimeOffset value)
    {
        var database = new FakeDatabase();
        await using var context = database.Context(dialect);
        database.Returns(FakeDatabase.Row("Id", 1));

        context.Add(new Item { AtOffset = value });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return Assert.Single(database.Parameters);
    }

    [Fact]
    public async Task A_dialect_that_takes_any_offset_keeps_the_offset_it_was_given()
    {
        var parameters = await SavedParameters(TestDialects.Ansi, new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.FromHours(2)));

        Assert.Contains("+02:00", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_value_already_at_offset_zero_is_written_unchanged()
    {
        var parameters = await SavedParameters(UtcDialect, new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.Zero));

        Assert.Contains("2024-03-05T01:00:00", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_negative_offset_is_normalized_to_the_same_instant()
    {
        var value = new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.FromHours(-8));

        var parameters = await SavedParameters(UtcDialect, value);

        Assert.Contains("2024-03-05T09:00:00", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("-08:00", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_offset_that_is_not_a_whole_hour_is_normalized_to_the_same_instant()
    {
        var parameters = await SavedParameters(UtcDialect, new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.FromMinutes(345)));

        Assert.Contains("2024-03-04T19:15:00", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_largest_representable_instant_with_the_widest_offset_is_normalized_without_overflow()
    {
        var value = new DateTimeOffset(9999, 12, 31, 9, 0, 0, TimeSpan.FromHours(-14));

        var parameters = await SavedParameters(UtcDialect, value);

        Assert.Contains("9999-12-31T23:00:00", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_smallest_representable_instant_with_the_widest_offset_is_normalized_without_overflow()
    {
        var value = new DateTimeOffset(1, 1, 1, 14, 0, 0, TimeSpan.FromHours(14));

        var parameters = await SavedParameters(UtcDialect, value);

        Assert.Contains("0001-01-01T00:00:00", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public void The_normalization_is_an_explicit_capability_each_provider_declares()
    {
        Assert.False(TestDialects.Ansi.TypeMappings.WritesDateTimeOffsetAsUtc);
        Assert.True(UtcDialect.TypeMappings.WritesDateTimeOffsetAsUtc);
    }
}

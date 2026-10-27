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
}

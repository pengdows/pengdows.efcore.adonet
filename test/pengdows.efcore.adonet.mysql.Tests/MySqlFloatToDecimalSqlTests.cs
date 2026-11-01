using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     A float converted to decimal goes through its shortest text on MySQL, as .NET converts it: CAST(CONCAT(x) AS DECIMAL),
///     not CAST(x AS DECIMAL), which keeps the float's binary value (8.5999999046...). A decimal operand is unchanged.
/// </summary>
public class MySqlFloatToDecimalSqlTests
{
    [Fact]
    public async Task A_float_converted_to_decimal_is_cast_through_its_text()
    {
        var db = new FakeMySql();
        await using var context = db.Context<BlogContext>(o => new BlogContext(o));
        db.Connection.EnqueueScalarResult(0);

        _ = await context.Blogs.Where(b => (decimal)(float)b.Rating == 8.6m).CountAsync(TestContext.Current.CancellationToken);

        var sql = string.Join("\n", db.Commands);
        Assert.Contains("CAST(CONCAT(CAST(`b`.`Rating` AS FLOAT)) AS DECIMAL(65,30))", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_decimal_converted_to_decimal_is_not_routed_through_text()
    {
        var db = new FakeMySql();
        await using var context = db.Context<BlogContext>(o => new BlogContext(o));
        db.Connection.EnqueueScalarResult(0);

        _ = await context.Blogs.Where(b => (decimal)b.Rating == 8.6m).CountAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("CONCAT", string.Join("\n", db.Commands), StringComparison.Ordinal);
    }
}

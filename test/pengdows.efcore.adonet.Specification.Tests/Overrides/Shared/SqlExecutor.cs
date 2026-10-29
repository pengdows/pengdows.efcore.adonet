using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels.ConcurrencyModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace pengdows.efcore.adonet.Specification.Tests;

// EF 10.0.11's raw-SQL tests quote identifiers ISO's way ("Customers"), which a database that quotes its own way (MySQL's
// backticks) reads as strings. These write [Customers] and the test store quotes each name as the database does, as EF's
// other raw-SQL tests do; an interpolated query keeps its holes as parameters. Pomelo rewrites the same tests with
// backticks. Behaviour reference (not copied):
// https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/Query/SqlExecutorMySqlTest.cs
public abstract partial class SqlExecutorAdoNetTest<THost>
{
    private const string CountByCityAndTitle = "SELECT COUNT(*) FROM [Customers] WHERE [City] = {0} AND [ContactTitle] = {1}";

    private string Raw(string sql)
        => ((RelationalTestStore)Fixture.TestStore).NormalizeDelimitersInRawString(sql);

    private FormattableString Interpolated(string format, params object?[] arguments)
        => FormattableStringFactory.Create(Raw(format), arguments);

    public override async Task Query_with_DbParameters_interpolated(bool async)
    {
        var city = CreateDbParameter("city", "London");
        var contactTitle = CreateDbParameter("contactTitle", "Sales Representative");

        using var context = CreateContext();

        var actual = async
            ? await context.Database.ExecuteSqlInterpolatedAsync(Interpolated(CountByCityAndTitle, city, contactTitle))
            : context.Database.ExecuteSqlInterpolated(Interpolated(CountByCityAndTitle, city, contactTitle));

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_DbParameters_interpolated_2(bool async)
    {
        var city = CreateDbParameter("city", "London");
        var contactTitle = CreateDbParameter("contactTitle", "Sales Representative");

        using var context = CreateContext();

        var actual = async
            ? await context.Database.ExecuteSqlAsync(Interpolated(CountByCityAndTitle, city, contactTitle))
            : context.Database.ExecuteSql(Interpolated(CountByCityAndTitle, city, contactTitle));

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_dbParameter_with_name(bool async)
    {
        var city = CreateDbParameter("@city", "London");

        using var context = CreateContext();

        var sql = Raw("SELECT COUNT(*) FROM [Customers] WHERE [City] = @city");
        var actual = async
            ? await context.Database.ExecuteSqlRawAsync(sql, city)
            : context.Database.ExecuteSqlRaw(sql, city);

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_dbParameters_mixed(bool async)
    {
        var city = "London";
        var contactTitle = "Sales Representative";

        var cityParameter = CreateDbParameter("@city", city);
        var contactTitleParameter = CreateDbParameter("@contactTitle", contactTitle);

        using var context = CreateContext();

        var first = Raw("SELECT COUNT(*) FROM [Customers] WHERE [City] = {0} AND [ContactTitle] = @contactTitle");
        var actual = async
            ? await context.Database.ExecuteSqlRawAsync(first, city, contactTitleParameter)
            : context.Database.ExecuteSqlRaw(first, city, contactTitleParameter);

        Assert.Equal(-1, actual);

        var second = Raw("SELECT COUNT(*) FROM [Customers] WHERE [City] = @city AND [ContactTitle] = {1}");
        actual = async
            ? await context.Database.ExecuteSqlRawAsync(second, cityParameter, contactTitle)
            : context.Database.ExecuteSqlRaw(second, cityParameter, contactTitle);

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_parameters(bool async)
    {
        var city = "London";
        var contactTitle = "Sales Representative";

        using var context = CreateContext();

        var actual = async
            ? await context.Database.ExecuteSqlRawAsync(Raw(CountByCityAndTitle), city, contactTitle)
            : context.Database.ExecuteSqlRaw(Raw(CountByCityAndTitle), city, contactTitle);

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_parameters_custom_converter(bool async)
    {
        var city = new City { Name = "London" };
        var contactTitle = "Sales Representative";

        using var context = CreateContext();

        var actual = async
            ? await context.Database.ExecuteSqlAsync(Interpolated(CountByCityAndTitle, city, contactTitle))
            : context.Database.ExecuteSql(Interpolated(CountByCityAndTitle, city, contactTitle));

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_parameters_interpolated(bool async)
    {
        var city = "London";
        var contactTitle = "Sales Representative";

        using var context = CreateContext();

        var actual = async
            ? await context.Database.ExecuteSqlInterpolatedAsync(Interpolated(CountByCityAndTitle, city, contactTitle))
            : context.Database.ExecuteSqlInterpolated(Interpolated(CountByCityAndTitle, city, contactTitle));

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_parameters_interpolated_2(bool async)
    {
        var city = "London";
        var contactTitle = "Sales Representative";

        using var context = CreateContext();

        var actual = async
            ? await context.Database.ExecuteSqlAsync(Interpolated(CountByCityAndTitle, city, contactTitle))
            : context.Database.ExecuteSql(Interpolated(CountByCityAndTitle, city, contactTitle));

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_positional_dbParameter_with_name(bool async)
    {
        var city = CreateDbParameter("@city", "London");

        using var context = CreateContext();

        var sql = Raw("SELECT COUNT(*) FROM [Customers] WHERE [City] = {0}");
        var actual = async
            ? await context.Database.ExecuteSqlRawAsync(sql, city)
            : context.Database.ExecuteSqlRaw(sql, city);

        Assert.Equal(-1, actual);
    }

    public override async Task Query_with_positional_dbParameter_without_name(bool async)
    {
        var city = CreateDbParameter(name: null!, value: "London");

        using var context = CreateContext();

        var sql = Raw("SELECT COUNT(*) FROM [Customers] WHERE [City] = {0}");
        var actual = async
            ? await context.Database.ExecuteSqlRawAsync(sql, city)
            : context.Database.ExecuteSqlRaw(sql, city);

        Assert.Equal(-1, actual);
    }
}

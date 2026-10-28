using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     A database's compatibility level decides the T-SQL it takes: TRIM(x) and TRIM(characters FROM x) need 140 (SQL Server
///     2017), TRIM(LEADING ...) and two-argument LTRIM/RTRIM 160 (2022), the JSON functions and OPENJSON 130 (2016). The
///     dialect for a level writes what that level has and leaves the rest untranslated. Provider-only: compatibility levels
///     are SQL Server's. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/Translators/SqlServerStringMethodTranslator.cs
/// </summary>
public class SqlServerCompatibilityLevelTests
{
    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class RowContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }

    private static string Sql(SqlServerDialect dialect, Func<IQueryable<Row>, IQueryable> query)
    {
        using var context = new RowContext(new DbContextOptionsBuilder<RowContext>()
            .UseAdoNet(Microsoft.Data.SqlClient.SqlClientFactory.Instance, "Server=.;Database=x", dialect)
            .Options);
        return query(context.Rows).ToQueryString();
    }

    private static void Untranslated(SqlServerDialect dialect, Func<IQueryable<Row>, IQueryable> query)
        => Assert.Contains("could not be translated", Assert.Throws<InvalidOperationException>(() => Sql(dialect, query)).Message);

    [Fact]
    public void The_default_dialect_is_compatibility_level_160()
    {
        Assert.Equal(160, SqlServerDialect.Instance.CompatibilityLevel);
        Assert.Same(SqlServerDialect.Instance, SqlServerDialect.ForCompatibilityLevel(160));
        Assert.Contains("LTRIM([r].[Name], N'xy')", Sql(SqlServerDialect.Instance, q => q.Where(r => r.Name.TrimStart('x', 'y') == "a")));
        Assert.Contains("TRIM(LEADING N'x' FROM [r].[Name])", Sql(SqlServerDialect.Instance, q => q.Where(r => r.Name.TrimStart('x') == "a")));
    }

    [Fact]
    public void Level_140_trims_both_ends_of_characters_but_not_one_end()
    {
        var level140 = SqlServerDialect.ForCompatibilityLevel(140);

        Assert.Contains("TRIM(N'xy' FROM [r].[Name])", Sql(level140, q => q.Where(r => r.Name.Trim('x', 'y') == "a")));
        Assert.Contains("TRIM(N'x' FROM [r].[Name])", Sql(level140, q => q.Where(r => r.Name.Trim('x') == "a")));
        Assert.Contains("TRIM([r].[Name])", Sql(level140, q => q.Where(r => r.Name.Trim() == "a")));
        Untranslated(level140, q => q.Where(r => r.Name.TrimStart('x', 'y') == "a"));
        Untranslated(level140, q => q.Where(r => r.Name.TrimEnd('x') == "a"));
    }

    [Fact]
    public void Level_130_trims_spaces_with_ltrim_and_rtrim_and_reads_json()
    {
        var level130 = SqlServerDialect.ForCompatibilityLevel(130);

        Assert.Contains("LTRIM(RTRIM([r].[Name]))", Sql(level130, q => q.Where(r => r.Name.Trim() == "a")));
        Untranslated(level130, q => q.Where(r => r.Name.Trim('x', 'y') == "a"));
        Assert.NotNull(level130.Sql.Json);
    }

    [Fact]
    public void Below_130_there_are_no_json_functions()
    {
        var level120 = SqlServerDialect.ForCompatibilityLevel(120);

        Assert.Null(level120.Sql.Json);
        var ids = new[] { 1, 2 };
        Assert.DoesNotContain("OPENJSON", Sql(level120, q => q.Where(r => ids.Contains(r.Id))));
    }

    [Fact]
    public void A_level_older_than_sql_server_2008_is_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SqlServerDialect.ForCompatibilityLevel(90));
}

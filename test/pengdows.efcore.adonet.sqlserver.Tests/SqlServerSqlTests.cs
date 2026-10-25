using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Bulk;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlserver.Tests;

public class SqlServerSqlTests
{
    private static BlogContext Context(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        configure(builder);
        return new BlogContext(builder.Options);
    }

    private static string PagedQuery(BlogContext context)
    {
        var name = "x";
        return context.Blogs.Where(b => b.Name == name).OrderBy(b => b.Id).Skip(5).Take(10).ToQueryString();
    }

    [Fact]
    public void Delimits_identifiers_with_brackets()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.Equal("[a]]b]", context.GetService<ISqlGenerationHelper>().DelimitIdentifier("a]b"));
    }

    [Theory]
    [InlineData(typeof(bool), "bit")]
    [InlineData(typeof(byte), "tinyint")]
    [InlineData(typeof(short), "smallint")]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "bigint")]
    [InlineData(typeof(float), "real")]
    [InlineData(typeof(double), "float")]
    [InlineData(typeof(decimal), "decimal(18,2)")]
    [InlineData(typeof(string), "nvarchar(max)")]
    [InlineData(typeof(DateTime), "datetime2")]
    [InlineData(typeof(DateTimeOffset), "datetimeoffset")]
    [InlineData(typeof(DateOnly), "date")]
    [InlineData(typeof(TimeOnly), "time")]
    [InlineData(typeof(TimeSpan), "time")]
    [InlineData(typeof(Guid), "uniqueidentifier")]
    [InlineData(typeof(byte[]), "varbinary(max)")]
    public void Names_store_types_as_the_official_provider_does(Type clrType, string storeType)
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.Equal(storeType, context.GetService<IRelationalTypeMappingSource>().FindMapping(clrType)!.StoreType);
    }

    [Theory]
    [InlineData("money", typeof(decimal))]
    [InlineData("smallmoney", typeof(decimal))]
    [InlineData("bit", typeof(bool))]
    [InlineData("tinyint", typeof(byte))]
    [InlineData("datetime", typeof(DateTime))]
    [InlineData("datetime2(3)", typeof(DateTime))]
    [InlineData("smalldatetime", typeof(DateTime))]
    [InlineData("datetimeoffset", typeof(DateTimeOffset))]
    [InlineData("uniqueidentifier", typeof(Guid))]
    [InlineData("nvarchar(max)", typeof(string))]
    [InlineData("ntext", typeof(string))]
    [InlineData("xml", typeof(string))]
    [InlineData("image", typeof(byte[]))]
    [InlineData("rowversion", typeof(byte[]))]
    [InlineData("timestamp", typeof(byte[]))]
    [InlineData("float", typeof(double))]
    [InlineData("time", typeof(TimeOnly))]
    public void Knows_its_store_types(string storeType, Type clrType)
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(storeType)!;

        Assert.Equal(clrType, mapping.ClrType);
        Assert.Equal(storeType, mapping.StoreType);
    }

    [Fact]
    public void Sizes_unbounded_string_parameters_as_the_official_provider_does()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));
        var mappings = context.GetService<ITypeMappingDialect>();

        Assert.Equal(4000, mappings.StringParameterSize(unicode: true));
        Assert.Equal(8000, mappings.StringParameterSize(unicode: false));
    }

    public static TheoryData<string, object, string> Literals => new()
    {
        { "smallint", (short)20, "CAST(20 AS smallint)" },
        { "tinyint", (byte)2, "CAST(2 AS tinyint)" },
        { "bigint", 2L, "CAST(2 AS bigint)" },
        { "real", 1.5f, "CAST(1.5 AS real)" },
        { "float", 100.0, "100.0E0" },
        { "float", 1e20, "1E+20" },
        { "datetime", new DateTime(1998, 1, 2, 3, 4, 5), "'1998-01-02T03:04:05.000'" },
        { "smalldatetime", new DateTime(1998, 1, 2, 3, 4, 5), "'1998-01-02T03:04:05'" },
        { "datetime2", new DateTime(1998, 1, 2, 3, 4, 5), "'1998-01-02T03:04:05.0000000'" },
        { "datetime2(3)", new DateTime(1998, 1, 2, 3, 4, 5), "'1998-01-02T03:04:05.000'" },
        { "date", new DateTime(1998, 1, 2), "'1998-01-02'" },
        { "date", new DateOnly(1998, 1, 2), "'1998-01-02'" },
        { "time", new TimeOnly(3, 4, 5), "'03:04:05'" },
        { "time", new TimeOnly(3, 4, 5, 6), "'03:04:05.006'" },
        { "time", new TimeSpan(3, 4, 5), "'03:04:05'" },
        { "datetimeoffset", new DateTimeOffset(1998, 1, 2, 3, 4, 5, TimeSpan.FromHours(1)), "'1998-01-02T03:04:05.0000000+01:00'" },
        { "varbinary(max)", new byte[] { 1, 0xAB }, "0x01AB" },
        { "decimal(18,2)", 1.5m, "1.5" }
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void Writes_literals_as_the_official_provider_does(string storeType, object value, string literal)
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(value.GetType(), storeType)!;

        Assert.Equal(literal, mapping.GenerateSqlLiteral(value));
    }

    [Fact]
    public void Query_strings_declare_their_parameters_as_runnable_t_sql()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));
        var name = "it's";
        var rating = 2.5m;
        string? missing = null;

        var sql = context.Blogs.Where(b => b.Name == name && b.Rating > rating && b.Name != missing).ToQueryString().ReplaceLineEndings("\n");

        Assert.StartsWith("DECLARE @name nvarchar(4000) = N'it''s';\nDECLARE @rating decimal(18,2) = 2.5;\n", sql);
        Assert.Contains("\n\nSELECT [b].[Id]", sql);
    }

    [Fact]
    public void Prefers_exists_to_a_nullable_in()
        => Assert.True(SqlServerDialect.Instance.Sql.PreferExistsToIn);

    [Fact]
    public void Takes_an_existing_connection()
    {
        using var connection = new SqlConnection("Server=localhost");
        using var context = Context(b => b.UseAdoNetSqlServer(connection));

        Assert.Same(connection, context.Database.GetDbConnection());
        Assert.Same(SqlServerDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void Takes_the_connection_string_later()
    {
        using var context = Context(b => b.UseAdoNetSqlServer());

        context.Database.SetConnectionString("Server=later");

        Assert.IsType<SqlConnection>(context.Database.GetDbConnection());
        Assert.Equal("Server=later", context.Database.GetDbConnection().ConnectionString);
        Assert.Same(SqlServerDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void Uses_top_without_an_offset()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        var sql = context.Blogs.Take(3).ToQueryString();

        Assert.Matches(@"SELECT TOP\(@p\w*\) ", sql);
        Assert.DoesNotContain("FETCH", sql);
    }

    [Fact]
    public void Orders_by_a_constant_when_offset_has_no_ordering()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.Matches(@"ORDER BY \(SELECT 1\)\s+OFFSET @p\w* ROWS", context.Blogs.Skip(3).ToQueryString());
        Assert.Matches(@"OFFSET @p\w* ROWS FETCH NEXT @p\w* ROWS ONLY", PagedQuery(context));
    }

    [Fact]
    public void Converts_between_bit_values_and_conditions()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        var where = context.Blogs.Where(b => b.IsActive && !b.IsActive).ToQueryString();
        var select = context.Blogs.Select(b => new { Big = b.Rating > 5, b.IsActive }).ToQueryString();

        Assert.Contains("WHERE [b].[IsActive] = CAST(1 AS bit) AND [b].[IsActive] = CAST(0 AS bit)", where);
        Assert.Contains("CASE\n    WHEN [b].[Rating] > 5.0 THEN CAST(1 AS bit)\n    ELSE CAST(0 AS bit)\nEND AS [Big]", select.ReplaceLineEndings("\n"));
        Assert.Contains("SELECT ~[b].[IsActive]", context.Blogs.Select(b => !b.IsActive).ToQueryString());
    }

    [Fact]
    public void Long_count_uses_count_big()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.Contains("COUNT_BIG(*)", context.Blogs.GroupBy(b => b.Name).Select(g => g.LongCount()).ToQueryString());
        Assert.DoesNotContain("COUNT_BIG", context.Blogs.GroupBy(b => b.Name).Select(g => g.Count()).ToQueryString());
    }

    [Fact]
    public void Bulk_loads_use_the_native_loader()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.IsType<SqlServerBulkLoader>(context.GetService<IAdoNetBulkLoader>());
    }
}

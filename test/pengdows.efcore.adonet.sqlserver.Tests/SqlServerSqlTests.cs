using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
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

    // DATEDIFF has quarter, week and nanosecond parts, but no tick: a tick difference is left untranslated.
    [Fact]
    public void Date_differences_have_quarters_weeks_and_nanoseconds_but_no_ticks()
    {
        var functions = SqlServerDialect.Instance.Sql.Functions;

        Assert.Equal("DATEDIFF(quarter, {0}, {1})", functions.DateDiff(DateDiffUnit.Quarter));
        Assert.Equal("DATEDIFF(week, {0}, {1})", functions.DateDiff(DateDiffUnit.Week));
        Assert.Equal("DATEDIFF(nanosecond, {0}, {1})", functions.DateDiff(DateDiffUnit.Nanosecond));
        Assert.Null(functions.DateDiff(DateDiffUnit.Tick));
    }

    [Fact]
    public void Delimits_identifiers_with_brackets()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.Equal("[a]]b]", context.GetService<ISqlGenerationHelper>().DelimitIdentifier("a]b"));
    }

    [Fact]
    public void Declares_spatial_parameter_binding_explicitly()
    {
        Assert.Equal(SpatialParameterBinding.UserDefinedType, SqlServerDialect.Instance.Sql.Spatial!.ParameterBinding);
    }

    [Fact]
    public void Declares_json_boolean_update_conversion_explicitly()
    {
        Assert.Equal("CONVERT(bit, {0})", SqlServerDialect.Instance.Sql.Json!.SetBooleanValue);
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

    public static TheoryData<Type, StoreTypeFacets, string?> FacetedStoreTypes => new()
    {
        { typeof(string), new StoreTypeFacets(Size: 50), "nvarchar(50)" },
        { typeof(string), new StoreTypeFacets(Size: 50, Unicode: false), "varchar(50)" },
        { typeof(string), new StoreTypeFacets(Size: 5, FixedLength: true), "nchar(5)" },
        { typeof(string), new StoreTypeFacets(Size: 5, FixedLength: true, Unicode: false), "char(5)" },
        { typeof(string), new StoreTypeFacets(Size: 4001), "nvarchar(max)" },
        { typeof(string), new StoreTypeFacets(Size: 8000, Unicode: false), "varchar(8000)" },
        { typeof(string), new StoreTypeFacets(Size: 8001, Unicode: false), "varchar(max)" },
        { typeof(string), new StoreTypeFacets(Unicode: false), "varchar(max)" },
        { typeof(string), new StoreTypeFacets(), null },
        { typeof(byte[]), new StoreTypeFacets(Size: 32, FixedLength: true), "binary(32)" },
        { typeof(byte[]), new StoreTypeFacets(Size: 32), "varbinary(32)" },
        { typeof(byte[]), new StoreTypeFacets(Size: 8001), "varbinary(max)" },
        { typeof(decimal), new StoreTypeFacets(Precision: 10), "decimal(10,0)" }
    };

    [Theory]
    [MemberData(nameof(FacetedStoreTypes))]
    public void Names_faceted_store_types(Type clrType, StoreTypeFacets facets, string? storeType)
        => Assert.Equal(storeType, SqlServerDialect.Instance.TypeMappings.GetStoreType(clrType, facets));

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
        { "float", double.Epsilon, "(2.2250738585072014E-308 / 4503599627370496E0)" },
        { "float", -1e-310, "(-4.503599627370482E-295 / 4503599627370496E0)" },
        { "float", 2.2250738585072014E-308, "2.2250738585072014E-308" },
        { "real", 0.1f, "CAST(0.10000000149011612 AS real)" },
        { "datetime", new DateTime(1998, 1, 2, 3, 4, 5), "'1998-01-02T03:04:05.000'" },
        { "smalldatetime", new DateTime(1998, 1, 2, 3, 4, 5), "'1998-01-02T03:04:05'" },
        { "datetime2", new DateTime(1998, 1, 2, 3, 4, 5), "'1998-01-02T03:04:05.0000000'" },
        { "datetime2(3)", new DateTime(1998, 1, 2, 3, 4, 5), "'1998-01-02T03:04:05.000'" },
        { "date", new DateTime(1998, 1, 2), "'1998-01-02'" },
        { "date", new DateOnly(1998, 1, 2), "'1998-01-02'" },
        { "time", new TimeOnly(3, 4, 5), "'03:04:05'" },
        { "time", new TimeOnly(3, 4, 5, 6), "'03:04:05.006'" },
        { "time", new TimeSpan(3, 4, 5), "'03:04:05'" },
        { "time", new TimeSpan(1), "'00:00:00.0000001'" },
        { "time(3)", new TimeSpan(0, 3, 4, 5, 6), "'03:04:05.006'" },
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

    [Theory]
    [InlineData(-1L)]
    [InlineData(864_000_000_000L)]
    public void Time_spans_outside_a_day_are_refused_as_time_literals(long ticks)
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(TimeSpan), "time")!;

        Assert.Throws<ArgumentOutOfRangeException>(() => mapping.GenerateSqlLiteral(new TimeSpan(ticks)));
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
    public void Query_strings_declare_values_longer_than_a_bounded_type_as_max()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));
        var name = new string('x', 4001);

        var sql = context.Blogs.Where(b => b.Name == name).ToQueryString();

        Assert.StartsWith("DECLARE @name nvarchar(max) = N'xxx", sql);
    }

    [Fact]
    public void Query_strings_declare_a_null_with_the_type_of_its_parameter()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));
        using var command = new SqlCommand("SELECT @name, @when");
        command.Parameters.Add(new SqlParameter("@name", System.Data.SqlDbType.VarChar, 30) { Value = DBNull.Value });
        command.Parameters.Add(new SqlParameter("@when", System.Data.SqlDbType.DateTimeOffset) { Value = DBNull.Value });

        Assert.StartsWith(
            "DECLARE @name varchar(30) = NULL;\nDECLARE @when datetimeoffset = NULL;\n",
            context.GetService<IRelationalQueryStringFactory>().Create(command).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Query_strings_refuse_a_value_with_no_sql_literal_rather_than_write_its_text()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));
        using var command = new SqlCommand("SELECT @value");
        command.Parameters.Add(new SqlParameter("@value", System.Data.SqlDbType.NVarChar) { Value = new Unmapped() });

        var error = Assert.Throws<NotSupportedException>(() => context.GetService<IRelationalQueryStringFactory>().Create(command));

        Assert.Contains("@value", error.Message);
    }

    private sealed class Unmapped
    {
        public override string ToString()
            => "'; DROP TABLE y; --";
    }

    // CHARINDEX('', x) is 0 where .NET's IndexOf("") is 0 (position 1), so an empty pattern is special-cased; a null string is
    // still null. Covers EF's Select_with_complex_expression_that_can_be_funcletized; runs there on all six databases.
    [Fact]
    public void Position_of_an_empty_pattern_in_a_null_string_is_null()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        var sql = context.Blogs.Select(b => b.Name.IndexOf("")).ToQueryString();

        Assert.Contains("CASE WHEN [b].[Name] IS NULL THEN NULL WHEN DATALENGTH(N'') = 0 THEN 1", sql);
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
    public void Translates_sql_server_date_parts()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        var sql = context.Blogs
            .Where(blog => blog.CreatedAt.Date == new DateTime(2024, 1, 2)
                && blog.CreatedAt.Microsecond != 0
                && blog.CreatedAt.Nanosecond != 0)
            .ToQueryString();

        Assert.Contains("CONVERT(date, [b].[CreatedAt])", sql);
        Assert.Contains("DATEPART(microsecond, [b].[CreatedAt]) % 1000", sql);
        Assert.Contains("DATEPART(nanosecond, [b].[CreatedAt]) % 1000", sql);
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

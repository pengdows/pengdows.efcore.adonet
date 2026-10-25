namespace pengdows.efcore.adonet.postgresql.Tests;

public class PostgreSqlLiteralTests
{
    public static TheoryData<object, string, string> Literals => new()
    {
        { new byte[] { 0, 1, 255 }, "BYTEA", @"'\x0001FF'::bytea" },
        { Array.Empty<byte>(), "BYTEA", @"'\x'::bytea" },
        { new DateTimeOffset(2024, 3, 5, 1, 0, 0, 500, TimeSpan.FromHours(2)), "TIMESTAMP WITH TIME ZONE", "TIMESTAMPTZ '2024-03-05 01:00:00.5+02:00'" },
        { new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.FromHours(-5.5)), "TIMESTAMP WITH TIME ZONE", "TIMESTAMPTZ '2024-03-05 01:00:00-05:30'" },
        { new TimeSpan(0, 1, 2, 3, 500), "INTERVAL", "INTERVAL '3723500000 microseconds'" },
        { new TimeSpan(2, 1, 0, 0), "INTERVAL", "INTERVAL '176400000000 microseconds'" },
        { -new TimeSpan(1, 1, 30, 0), "INTERVAL", "INTERVAL '-91800000000 microseconds'" }
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void Writes_values_postgresql_reads_as_the_column_type(object value, string storeType, string literal)
        => Assert.Equal(literal, PostgreSqlDialect.Instance.TypeMappings.FormatLiteral(value, storeType, "unused"));
}

/// <summary>PostgreSQL folds unquoted names to lower case: only other names, and reserved words, need quotes.</summary>
public class PostgreSqlIdentifierTests
{
    [Theory]
    [InlineData("c", "c")]
    [InlineData("c0", "c0")]
    [InlineData("_tmp$1", "_tmp$1")]
    [InlineData("Customers", "\"Customers\"")]
    [InlineData("user", "\"user\"")]
    [InlineData("select", "\"select\"")]
    [InlineData("1st", "\"1st\"")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    [InlineData("éclair", "\"éclair\"")]
    public void Identifiers_are_quoted_only_where_postgresql_needs_it(string identifier, string sql)
    {
        using var context = new pengdows.efcore.adonet.Testing.BlogContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<pengdows.efcore.adonet.Testing.BlogContext>()
                .UseAdoNetPostgreSql("Host=localhost").Options);

        Assert.Equal(sql, Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Storage.ISqlGenerationHelper>(context).DelimitIdentifier(identifier));
    }
}

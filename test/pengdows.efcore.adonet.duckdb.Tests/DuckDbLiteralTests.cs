using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.duckdb.Tests;

public class DuckDbMigrationsTests
{
    [Fact]
    public void A_key_sequence_default_names_the_sequence_delimited_and_in_its_schema()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetDuckDb("Data Source=:memory:").Options);
        var id = new AddColumnOperation { Name = "Id", Table = "O'Brien", Schema = "s", ClrType = typeof(int) };
        id.AddAnnotation(AdoNetAnnotationNames.Identity, true);
        var create = new CreateTableOperation { Name = "O'Brien", Schema = "s", Columns = { id } };

        var sql = string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate([create]).Select(c => c.CommandText));

        Assert.Contains("DEFAULT nextval('\"s\".\"O''Brien_Id_seq\"')", sql);
    }

    /// <summary>DuckDB has no ALTER TABLE ... ADD FOREIGN KEY: the generator says so, naming the key, before the engine's own error.</summary>
    [Fact]
    public void Adding_a_foreign_key_to_an_existing_table_is_refused_by_name()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetDuckDb("Data Source=:memory:").Options);
        var add = new AddForeignKeyOperation
        {
            Name = "FK_\"x\"; DROP", Table = "Child", Columns = ["ParentId"], PrincipalTable = "Parent", PrincipalColumns = ["Id"]
        };

        var refused = Assert.Throws<NotSupportedException>(() => context.GetService<IMigrationsSqlGenerator>().Generate([add]));

        Assert.Contains("FK_\"x\"; DROP", refused.Message);
        Assert.Contains("Child", refused.Message);
    }
}

public class DuckDbLiteralTests
{
    [Theory]
    [InlineData(new byte[] { 0, 1, 255 }, @"'\x00\x01\xFF'::BLOB")]
    [InlineData(new byte[0], "''::BLOB")]
    public void Blobs_are_escaped_bytes(byte[] value, string literal)
        => Assert.Equal(literal, DuckDbDialect.Instance.TypeMappings.FormatLiteral(value, "BLOB", "unused"));

    [Fact]
    public void Date_offsets_keep_their_offset()
        => Assert.Equal(
            "TIMESTAMPTZ '2024-03-05 01:00:00.5+02:00'",
            DuckDbDialect.Instance.TypeMappings.FormatLiteral(
                new DateTimeOffset(2024, 3, 5, 1, 0, 0, 500, TimeSpan.FromHours(2)), "TIMESTAMP WITH TIME ZONE", "unused"));
}

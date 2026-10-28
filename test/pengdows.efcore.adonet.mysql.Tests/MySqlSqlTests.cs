using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

public class MySqlSqlTests
{
    [Fact]
    public void Session_setup_matches_the_string_literal_escape_mode()
    {
        Assert.EndsWith("'ANSI_QUOTES')", MySqlDialect.Instance.Sql.SessionSetup);
        Assert.EndsWith("'ANSI_QUOTES', 'NO_BACKSLASH_ESCAPES')", MySqlDialect.NoBackslashEscapes.Sql.SessionSetup);
    }

    [Fact]
    public void Connection_options_disable_affected_rows_mode_for_concurrency()
    {
        Assert.Equal("false", MySqlDialect.Instance.Sql.ConnectionOptions["Use Affected Rows"]);
        Assert.Equal("false", MySqlDialect.NoBackslashEscapes.Sql.ConnectionOptions["Use Affected Rows"]);
    }

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

    public class Server
    {
        public int Id { get; set; }
        public System.Net.IPAddress? Address { get; set; }
    }

    private sealed class ServerContext(DbContextOptions<ServerContext> options) : DbContext(options)
    {
        public DbSet<Server> Servers => Set<Server>();
    }

    // CAST(x AS CHAR(n)) takes the connection's collation as strongly as a column has its own, so against a column in another
    // collation it fails ("Illegal mix of collations": MariaDB 11's utf8mb4_uca1400_ai_ci beside MySqlConnector's
    // utf8mb4_general_ci). CONCAT yields to the column, and LEFT keeps the truncation to n.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_sized_text_cast_yields_to_the_columns_collation(bool mariaDb)
    {
        using var context = new ServerContext(new DbContextOptionsBuilder<ServerContext>()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance))
            .Options);

        var sql = context.Servers.Where(s => s.Address == System.Net.IPAddress.Loopback).ToQueryString();

        Assert.Contains("= LEFT(CONCAT('127.0.0.1'), 45)", sql);
    }

    [Fact]
    public void Uses_backticks_and_a_limit_for_offset_only()
    {
        using var context = Context(b => b.UseAdoNetMySql("Server=localhost"));

        var sql = context.Blogs.OrderBy(b => b.Id).Skip(5).ToQueryString();

        Assert.Contains("FROM \"Blogs\" AS \"b\"", sql);
        Assert.Matches(@"LIMIT 18446744073709551615\s+OFFSET @p", sql);
    }
}

/// <summary>
///     SingleStore speaks MySQL but accepts SAVEPOINT and ROLLBACK TO SAVEPOINT and ignores them, so rolled-back work is
///     committed. MySqlConnector reports savepoints there anyway (it declined to tell servers apart:
///     https://github.com/mysql-net/MySqlConnector/issues/1677), so the dialect says there are none.
/// </summary>
public class SingleStoreSavepointTests
{
    [Fact]
    public async Task SingleStore_has_no_savepoints_whatever_the_driver_reports()
    {
        await using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(MySqlDialect.SingleStore))
            .Options);

        Assert.Equal("SingleStore", MySqlDialect.SingleStore.Name);
        Assert.Null(MySqlDialect.SingleStore.Sql.Savepoints);
        Assert.NotNull(MySqlDialect.Instance.Sql.Savepoints);
        Assert.Same(MySqlDialect.SingleStore, context.GetService<IAdoNetDialect>());
    }
}

/// <summary>
///     MySQL takes no literal default on a TEXT, BLOB, JSON or GEOMETRY column, but since 8.0.13 takes an expression default:
///     a default on one of those is written in parentheses; other columns keep the plain literal. Behaviour reference:
///     Pomelo's MySqlMigrationsSqlGenerator.DefaultValue (adapted).
/// </summary>
public class MySqlDefaultValueTests
{
    private static string Sql(string columnType, object? defaultValue = null, string? defaultValueSql = null)
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetMySql("Server=localhost").Options);
        var operation = new Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation
        {
            Table = "T", Name = "C", ClrType = typeof(string), ColumnType = columnType, DefaultValue = defaultValue, DefaultValueSql = defaultValueSql
        };
        return string.Join("", context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator>().Generate([operation]).Select(c => c.CommandText));
    }

    [Theory]
    [InlineData("LONGTEXT")]
    [InlineData("text")]
    [InlineData("MEDIUMBLOB")]
    [InlineData("JSON")]
    public void A_default_on_a_text_blob_or_json_column_is_an_expression(string columnType)
        => Assert.Contains($"{columnType} DEFAULT ('Banana Joe')", Sql(columnType, "Banana Joe"));

    [Fact]
    public void A_default_sql_on_a_text_column_is_parenthesized_once()
    {
        Assert.Contains("LONGTEXT DEFAULT (UUID())", Sql("LONGTEXT", defaultValueSql: "UUID()"));
        Assert.Contains("LONGTEXT DEFAULT (UUID())", Sql("LONGTEXT", defaultValueSql: "(UUID())"));
    }

    [Fact]
    public void Other_columns_keep_a_plain_literal_default()
        => Assert.Contains("VARCHAR(20) DEFAULT 'Banana Joe'", Sql("VARCHAR(20)", "Banana Joe"));
}

/// <summary>
///     InnoDB wants an AUTO_INCREMENT column to lead an index; in a composite key led by another column (an owned
///     collection's owner key, then its own number) it doesn't, so the column gets a unique constraint of its own. Behaviour
///     reference: Pomelo's MySqlMigrationsSqlGenerator (adapted).
/// </summary>
public class MySqlAutoIncrementKeyTests
{
    private static string Sql(params string[] primaryKey)
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetMySql("Server=localhost").Options);
        var owner = new Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation { Table = "Lines", Name = "OwnerId", ClrType = typeof(int) };
        var id = new Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation { Table = "Lines", Name = "Id", ClrType = typeof(int) };
        id.AddAnnotation(AdoNetAnnotationNames.Identity, true);
        var create = new Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation
        {
            Name = "Lines",
            Columns = { owner, id },
            PrimaryKey = new Microsoft.EntityFrameworkCore.Migrations.Operations.AddPrimaryKeyOperation { Table = "Lines", Name = "PK_Lines", Columns = primaryKey }
        };
        return string.Join("", context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator>().Generate([create]).Select(c => c.CommandText));
    }

    [Fact]
    public void An_auto_increment_column_after_the_first_key_column_leads_a_unique_constraint()
        => Assert.Contains("UNIQUE (\"Id\")", Sql("OwnerId", "Id"));

    [Fact]
    public void An_auto_increment_column_that_leads_the_key_needs_nothing_more()
        => Assert.DoesNotContain("UNIQUE", Sql("Id", "OwnerId"));
}

/// <summary>
///     A column type named in the model is written as named: Pomelo writes HasColumnType("boolean") as the invalid
///     boolean(1) (https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2045). Upstream bug matrix:
///     docs/upstream-bugs/MATRIX.md.
/// </summary>
public class MySqlNamedColumnTypeTests
{
    public class Flagged
    {
        public int Id { get; set; }
        public bool Flag { get; set; }
        public bool Other { get; set; }
    }

    private sealed class FlaggedContext(DbContextOptions<FlaggedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Flagged>(b =>
            {
                b.Property(e => e.Flag).HasColumnType("boolean");
                b.Property(e => e.Other).HasColumnType("bool");
            });
    }

    [Fact]
    public void A_boolean_column_type_is_written_as_named()
    {
        using var context = new FlaggedContext(new DbContextOptionsBuilder<FlaggedContext>().UseAdoNetMySql("Server=localhost").Options);
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"Flag\" boolean NOT NULL", script);
        Assert.Contains("\"Other\" bool NOT NULL", script);
    }
}

/// <summary>
///     MariaDB speaks MySQL's dialect but has no LATERAL derived tables (MDEV-19078), writes a table value constructor's rows
///     as plain <c>(...)</c> without <c>ROW</c>, and has no JSON type to CAST to: JSON is an alias of LONGTEXT, and a JSON
///     function's result is what JSON_SET takes as JSON rather than as a string.
/// </summary>
public class MariaDbSqlTests
{
    [Fact]
    public void MariaDb_has_no_lateral_joins_rows_without_ROW_and_json_from_a_json_function()
    {
        Assert.Equal("MariaDB", MySqlDialect.MariaDb.Name);
        Assert.Null(MySqlDialect.MariaDb.Sql.LateralJoins);
        Assert.False(MySqlDialect.MariaDb.Sql.SupportsOuterReferencesInDerivedTables);
        Assert.True(MySqlDialect.Instance.Sql.SupportsOuterReferencesInDerivedTables);
        Assert.Equal("", MySqlDialect.MariaDb.Sql.ValuesRowPrefix);
        Assert.Equal("JSON_COMPACT({0})", MySqlDialect.MariaDb.Sql.Json!.FromText);
        Assert.Equal("JSON_EQUALS({0}, {1})", MySqlDialect.MariaDb.Sql.Json!.Equality);
        Assert.Null(MySqlDialect.Instance.Sql.Json!.Equality);
        Assert.NotNull(MySqlDialect.Instance.Sql.LateralJoins);
        Assert.Equal("ROW", MySqlDialect.Instance.Sql.ValuesRowPrefix);
    }

    // MariaDB's spatial functions: ST_GeomFromText without MySQL's axis-order option (its x is always the longitude),
    // ST_NumInteriorRings without the singular synonym, and no ST_IsValid; its REGEXP is PCRE, with no REGEXP_LIKE, and
    // (?-i) makes it case-sensitive whatever the collation.
    [Fact]
    public void MariaDb_writes_its_own_spatial_and_regex_functions()
    {
        var spatial = MySqlDialect.MariaDb.Sql.Spatial!;
        Assert.Equal("ST_GeomFromText({0}, {1})", spatial.FromText);
        Assert.Equal("ST_NumInteriorRings({0})", spatial.Functions["NumInteriorRings"]);
        Assert.Null(spatial.Functions["IsValid"]);
        Assert.Equal("CASE WHEN {1} = '' THEN TRUE ELSE {0} REGEXP CONCAT('(?-i)', {1}) END", MySqlDialect.MariaDb.Sql.Functions.RegexIsMatch);
        Assert.Contains("REGEXP_LIKE", MySqlDialect.Instance.Sql.Functions.RegexIsMatch);
    }

    [Fact]
    public void A_query_with_a_lateral_join_is_refused_on_MariaDb()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(MySqlDialect.MariaDb))
            .Options);

        Assert.ThrowsAny<InvalidOperationException>(
            () => context.Blogs
                .SelectMany(b => context.Posts.Where(p => p.Id > b.Id).OrderBy(p => p.Id).Take(1), (b, p) => new { b.Id, p.Title })
                .ToQueryString());
    }
}

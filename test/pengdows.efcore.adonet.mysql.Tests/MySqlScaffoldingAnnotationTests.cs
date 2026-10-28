using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Scaffolding reads back what the migrations write, as the annotations Pomelo's scaffolding gives: the database's default
///     character set and collation; a table's own character set, collation and options (an ENGINE other than the server's
///     default, and the options it was created with); a column's own character set and SRID (MySQL's COLUMNS.SRS_ID,
///     MariaDB's GEOMETRY_COLUMNS); an index's or key's prefix lengths; FULLTEXT and SPATIAL indexes and a FULLTEXT index's
///     parser. The catalog's rows are scripted through fakeDb, in the order the reader asks for them. Provider-only: MySQL's
///     catalog. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Scaffolding/Internal/MySqlDatabaseModelFactory.cs
/// </summary>
public class MySqlScaffoldingAnnotationTests
{
    private static Dictionary<string, object?> Row(params (string Name, object? Value)[] values)
        => values.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, object?> ColumnRow(
        string table, string name, long position, string type, string? charSet, string? tableCollation, object? srid = null)
        => Row(
            ("SCHEMA", "shop"), ("TABLE", table), ("NAME", name), ("POSITION", position), ("NULLABLE", "NO"), ("DEFAULT", null),
            ("DATA_TYPE", type.Split('(')[0].ToLowerInvariant()), ("COLUMN_TYPE", type.ToLowerInvariant()), ("EXTRA", ""),
            ("COLUMN_COMMENT", ""), ("GENERATION_EXPRESSION", ""), ("COLLATION_NAME", charSet == null ? null : charSet + "_bin"),
            ("TABLE_COLLATION", tableCollation), ("CHARACTER_SET_NAME", charSet), ("TABLE_CHARSET", tableCollation?.Split('_')[0]),
            ("SRS_ID", srid));

    private static Dictionary<string, object?> IndexRow(string table, string name, string column, long position, bool unique = false)
        => Row(
            ("SCHEMA", "shop"), ("TABLE", table), ("NAME", name), ("IS_UNIQUE", unique ? 1L : 0L), ("COLUMN", column),
            ("POSITION", position), ("IS_DESCENDING", 0L), ("FILTER", null));

    private static Dictionary<string, object?> StatisticsRow(string table, string name, string column, long position, long? subPart, string type)
        => Row(("TABLE", table), ("NAME", name), ("COLUMN", column), ("POSITION", position), ("SUB_PART", subPart), ("INDEX_TYPE", type));

    private static DatabaseModel Read(MySqlDialect dialect)
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=MySql" };

        // Tables, columns, primary keys, foreign keys and indexes, as every reader reads them.
        connection.EnqueueReaderResult([
            Row(("SCHEMA", "shop"), ("NAME", "Books"), ("TYPE", "BASE TABLE"), ("COMMENT", "")),
            Row(("SCHEMA", "shop"), ("NAME", "Places"), ("TYPE", "BASE TABLE"), ("COMMENT", ""))
        ]);
        connection.EnqueueReaderResult([
            ColumnRow("Books", "Code", 1, "VARCHAR(300)", "utf8mb4", "latin1_swedish_ci"),
            ColumnRow("Books", "Title", 2, "VARCHAR(300)", "latin1", "latin1_swedish_ci"),
            ColumnRow("Books", "Body", 3, "LONGTEXT", "latin1", "latin1_swedish_ci"),
            ColumnRow("Places", "Id", 1, "INT", null, "utf8mb4_0900_ai_ci"),
            ColumnRow("Places", "Location", 2, "POINT", null, "utf8mb4_0900_ai_ci", 4326L)
        ]);
        connection.EnqueueReaderResult([
            Row(("SCHEMA", "shop"), ("TABLE", "Books"), ("NAME", "PRIMARY"), ("TYPE", "PRIMARY KEY"), ("COLUMN", "Code"), ("POSITION", 1L)),
            Row(("SCHEMA", "shop"), ("TABLE", "Places"), ("NAME", "PRIMARY"), ("TYPE", "PRIMARY KEY"), ("COLUMN", "Id"), ("POSITION", 1L))
        ]);
        connection.EnqueueReaderResult(new List<Dictionary<string, object?>>());
        connection.EnqueueReaderResult([
            IndexRow("Books", "IX_Books_Title", "Title", 1),
            IndexRow("Books", "IX_Books_Body", "Body", 1),
            IndexRow("Places", "IX_Places_Location", "Location", 1)
        ]);

        // MySQL's own: the database's defaults, the tables' settings, the indexes' kinds and prefixes, a FULLTEXT's parser.
        connection.EnqueueReaderResult([
            Row(("CHARSET", "utf8mb4"), ("COLLATION", "utf8mb4_0900_ai_ci"), ("ENGINE", "InnoDB"))
        ]);
        connection.EnqueueReaderResult([
            Row(("NAME", "Books"), ("TABLE_COLLATION", "latin1_swedish_ci"), ("ENGINE", "MyISAM"), ("CREATE_OPTIONS", "row_format=DYNAMIC")),
            Row(("NAME", "Places"), ("TABLE_COLLATION", "utf8mb4_0900_ai_ci"), ("ENGINE", "InnoDB"), ("CREATE_OPTIONS", ""))
        ]);
        connection.EnqueueReaderResult([
            StatisticsRow("Books", "PRIMARY", "Code", 1, 20, "BTREE"),
            StatisticsRow("Books", "IX_Books_Title", "Title", 1, 10, "BTREE"),
            StatisticsRow("Books", "IX_Books_Body", "Body", 1, null, "FULLTEXT"),
            StatisticsRow("Places", "PRIMARY", "Id", 1, null, "BTREE"),
            StatisticsRow("Places", "IX_Places_Location", "Location", 1, 32, "SPATIAL")
        ]);
        connection.EnqueueReaderResult([
            Row(("Table", "Books"), ("Create Table",
                "CREATE TABLE \"Books\" (\n  \"Body\" longtext,\n  FULLTEXT KEY \"IX_Books_Body\" (\"Body\") /*!50100 WITH PARSER \"ngram\" */ \n)"))
        ]);

        connection.Open();
        return new AdoNetDatabaseModelFactory(dialect).Create(connection, new DatabaseModelFactoryOptions());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_databases_and_tables_character_sets_collations_and_options_read_back(bool mariaDb)
    {
        var model = Read(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance);
        var books = model.Tables.Single(t => t.Name == "Books");
        var places = model.Tables.Single(t => t.Name == "Places");

        Assert.Equal("utf8mb4", model["MySql:CharSet"]);
        Assert.Equal("utf8mb4_0900_ai_ci", model.Collation);
        Assert.Equal("latin1", books["MySql:CharSet"]);
        Assert.Equal("latin1_swedish_ci", books["Relational:Collation"]);
        Assert.Equal("ENGINE=MyISAM,row_format=DYNAMIC,", books["MySql:StoreOptions"]);
        Assert.Null(places["MySql:CharSet"]);
        Assert.Null(places["Relational:Collation"]);
        Assert.Null(places["MySql:StoreOptions"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_columns_own_character_set_and_srid_read_back(bool mariaDb)
    {
        var model = Read(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance);
        var books = model.Tables.Single(t => t.Name == "Books");
        var places = model.Tables.Single(t => t.Name == "Places");

        Assert.Equal("utf8mb4", books.Columns.Single(c => c.Name == "Code")["MySql:CharSet"]);
        Assert.Null(books.Columns.Single(c => c.Name == "Title")["MySql:CharSet"]);
        Assert.Equal(4326, places.Columns.Single(c => c.Name == "Location")["MySql:SpatialReferenceSystemId"]);
        Assert.Null(places.Columns.Single(c => c.Name == "Id")["MySql:SpatialReferenceSystemId"]);
    }

    // A spatial index's SUB_PART is the server's own (32 bytes), not a prefix the model gave.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Index_kinds_prefix_lengths_and_a_fulltext_parser_read_back(bool mariaDb)
    {
        var model = Read(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance);
        var books = model.Tables.Single(t => t.Name == "Books");
        var places = model.Tables.Single(t => t.Name == "Places");
        var title = books.Indexes.Single(i => i.Name == "IX_Books_Title");
        var body = books.Indexes.Single(i => i.Name == "IX_Books_Body");
        var location = places.Indexes.Single(i => i.Name == "IX_Places_Location");

        Assert.Equal(new[] { 20 }, books.PrimaryKey!["MySql:IndexPrefixLength"]);
        Assert.Null(places.PrimaryKey!["MySql:IndexPrefixLength"]);
        Assert.Equal(new[] { 10 }, title["MySql:IndexPrefixLength"]);
        Assert.Null(title["MySql:FullTextIndex"]);
        Assert.Equal(true, body["MySql:FullTextIndex"]);
        Assert.Equal("ngram", body["MySql:FullTextParser"]);
        Assert.Equal(true, location["MySql:SpatialIndex"]);
        Assert.Null(location["MySql:IndexPrefixLength"]);
    }
}

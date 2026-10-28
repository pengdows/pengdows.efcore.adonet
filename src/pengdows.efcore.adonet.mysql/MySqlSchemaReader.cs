// Behaviour reference (not copied): Pomelo's MySqlDatabaseModelFactory (MIT, Pomelo Foundation):
// https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Scaffolding/Internal/MySqlDatabaseModelFactory.cs

using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL's catalog: the database is the schema; a column's full type is <c>COLUMN_TYPE</c>, its generation and
///     <c>AUTO_INCREMENT</c> are in <c>EXTRA</c>, and its default is the bare value unless the server marks it an expression;
///     indexes are in <c>STATISTICS</c>, where unique constraints also appear (so only the primary key is read as a key); a
///     foreign key names its principal in <c>KEY_COLUMN_USAGE</c>; there are no sequences.
/// </summary>
internal sealed partial class MySqlSchemaReader(bool mariaDb = false) : InformationSchemaReader
{
    public static readonly MySqlSchemaReader Instance = new();

    /// <summary>MariaDB keeps a geometry column's SRID in GEOMETRY_COLUMNS, where MySQL has COLUMNS.SRS_ID.</summary>
    public static readonly MySqlSchemaReader MariaDb = new(mariaDb: true);

    public override bool ScaffoldsSchemas
        => false;

    // Every primary key is named PRIMARY; there are no filtered indexes.
    public override bool KeepsConstraintNames
        => false;

    public override bool ReadsIndexFilters
        => false;

    // A view's comment is the word VIEW.
    public override string TablesSql
        => "SELECT TABLE_SCHEMA AS \"SCHEMA\", TABLE_NAME AS \"NAME\", TABLE_TYPE AS \"TYPE\", "
            + "CASE WHEN TABLE_TYPE = 'VIEW' THEN NULL ELSE TABLE_COMMENT END AS \"COMMENT\" "
            + "FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = {schema} AND TABLE_TYPE IN ('BASE TABLE', 'VIEW')";

    public override string ColumnsSql
        => "SELECT c.TABLE_SCHEMA AS \"SCHEMA\", c.TABLE_NAME AS \"TABLE\", c.COLUMN_NAME AS \"NAME\", c.ORDINAL_POSITION AS \"POSITION\", "
            + "c.IS_NULLABLE AS \"NULLABLE\", c.COLUMN_DEFAULT AS \"DEFAULT\", c.DATA_TYPE, c.COLUMN_TYPE, c.EXTRA, c.COLUMN_COMMENT, "
            + "c.GENERATION_EXPRESSION, c.COLLATION_NAME, t.TABLE_COLLATION, c.CHARACTER_SET_NAME, "
            + "SUBSTRING_INDEX(t.TABLE_COLLATION, '_', 1) AS \"TABLE_CHARSET\", "
            + (mariaDb ? "g.SRID AS \"SRS_ID\" " : "c.SRS_ID AS \"SRS_ID\" ")
            + "FROM INFORMATION_SCHEMA.COLUMNS AS c JOIN INFORMATION_SCHEMA.TABLES AS t "
            + "ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME "
            + (mariaDb
                ? "LEFT JOIN INFORMATION_SCHEMA.GEOMETRY_COLUMNS AS g ON g.F_TABLE_SCHEMA = c.TABLE_SCHEMA "
                + "AND g.F_TABLE_NAME = c.TABLE_NAME AND g.F_GEOMETRY_COLUMN = c.COLUMN_NAME "
                : "")
            + "WHERE c.TABLE_SCHEMA = {schema}";

    public override string KeysSql
        => "SELECT TABLE_SCHEMA AS \"SCHEMA\", TABLE_NAME AS \"TABLE\", CONSTRAINT_NAME AS \"NAME\", 'PRIMARY KEY' AS \"TYPE\", "
            + "COLUMN_NAME AS \"COLUMN\", ORDINAL_POSITION AS \"POSITION\" "
            + "FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = {schema} AND CONSTRAINT_NAME = 'PRIMARY'";

    public override string ForeignKeysSql
        => "SELECT k.TABLE_SCHEMA AS \"SCHEMA\", k.TABLE_NAME AS \"TABLE\", k.CONSTRAINT_NAME AS \"NAME\", k.COLUMN_NAME AS \"COLUMN\", "
            + "k.ORDINAL_POSITION AS \"POSITION\", k.REFERENCED_TABLE_SCHEMA AS \"PRINCIPAL_SCHEMA\", "
            + "k.REFERENCED_TABLE_NAME AS \"PRINCIPAL_TABLE\", k.REFERENCED_COLUMN_NAME AS \"PRINCIPAL_COLUMN\", r.DELETE_RULE AS \"DELETE_RULE\" "
            + "FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS k JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS AS r "
            + "ON r.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA AND r.CONSTRAINT_NAME = k.CONSTRAINT_NAME AND r.TABLE_NAME = k.TABLE_NAME "
            + "WHERE k.TABLE_SCHEMA = {schema} AND k.REFERENCED_TABLE_NAME IS NOT NULL";

    // A functional index part has no column name.
    public override string IndexesSql
        => "SELECT TABLE_SCHEMA AS \"SCHEMA\", TABLE_NAME AS \"TABLE\", INDEX_NAME AS \"NAME\", "
            + "CASE WHEN NON_UNIQUE = 0 THEN 1 ELSE 0 END AS \"IS_UNIQUE\", COLUMN_NAME AS \"COLUMN\", SEQ_IN_INDEX AS \"POSITION\", "
            + "CASE WHEN COLLATION = 'D' THEN 1 ELSE 0 END AS \"IS_DESCENDING\", NULL AS \"FILTER\" "
            + "FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = {schema} AND INDEX_NAME <> 'PRIMARY' AND COLUMN_NAME IS NOT NULL";

    public override string? SequencesSql
        => null;

    protected override DatabaseColumn Column(IReadOnlyDictionary<string, object?> row)
    {
        var column = base.Column(row);
        var extra = Text(row, "EXTRA") ?? "";

        // MySQL keeps a generated column's expression in parentheses, and (bug 104294) escapes the quotes of its strings.
        if (Text(row, "GENERATION_EXPRESSION") is { Length: > 0 } generation)
        {
            column.ComputedColumnSql = OuterParentheses().Replace(generation, "$1").Replace(@"\'", "'", StringComparison.Ordinal);
            column.IsStored = extra.Contains("STORED GENERATED", StringComparison.OrdinalIgnoreCase);
            column.DefaultValueSql = null;
        }

        column.ValueGenerated = GeneratedValue(extra);
        column.Comment = Text(row, "COLUMN_COMMENT") is { Length: > 0 } comment ? comment : null;

        // A collation is the column's own only where it differs from its table's.
        column.Collation = Text(row, "COLLATION_NAME") is { } collation && (collation != Text(row, "TABLE_COLLATION")) ? collation : null;

        // So is a character set. A geometry column's SRID: MariaDB reports 0 for a column without one.
        if ((Text(row, "CHARACTER_SET_NAME") is { } charSet) && (charSet != Text(row, "TABLE_CHARSET")))
        {
            column[MySqlModelOptionsExtensions.CharSetAnnotation] = charSet;
        }

        var srid = row.TryGetValue("SRS_ID", out var value) && (value != null) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : (int?)null;
        if ((srid != null) && (!mariaDb || (srid != 0)))
        {
            column[MySqlModelOptionsExtensions.SpatialReferenceSystemAnnotation] = srid.Value;
        }

        return column;
    }

    // What INFORMATION_SCHEMA's ISO queries don't read: the database's defaults, each table's character set, collation and
    // options, each index's kind and prefix lengths and a FULLTEXT index's parser, as the annotations the migrations write.
    public override DatabaseModel Read(DbConnection connection, DatabaseModelFactoryOptions options, ISqlDialect sql)
    {
        var model = base.Read(connection, options, sql);
        var tables = model.Tables.Where(t => t is not DatabaseView).ToDictionary(t => t.Name, StringComparer.Ordinal);
        var defaults = Query(connection, DefaultsSql).FirstOrDefault() ?? new Dictionary<string, object?>();
        model[MySqlModelOptionsExtensions.CharSetAnnotation] = Text(defaults, "CHARSET");
        model.Collation = Text(defaults, "COLLATION");

        foreach (var row in Query(connection, TableSettingsSql))
        {
            if (tables.TryGetValue(Text(row, "NAME")!, out var table))
            {
                TableSettings(table, row, defaults);
            }
        }

        foreach (var index in Query(connection, IndexSettingsSql).GroupBy(r => (Table: Text(r, "TABLE")!, Name: Text(r, "NAME")!)))
        {
            if (tables.TryGetValue(index.Key.Table, out var table))
            {
                IndexSettings(table, index.Key.Name, index.OrderBy(r => Convert.ToInt64(r["POSITION"], CultureInfo.InvariantCulture)).ToList());
            }
        }

        foreach (var table in tables.Values.Where(t => t.Indexes.Any(i => i[MySqlFullTextExtensions.FullTextAnnotation] is true)))
        {
            FullTextParsers(connection, table);
        }

        return model;
    }

    private const string DefaultsSql
        = "SELECT DEFAULT_CHARACTER_SET_NAME AS \"CHARSET\", DEFAULT_COLLATION_NAME AS \"COLLATION\", @@default_storage_engine AS \"ENGINE\" "
            + "FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = DATABASE()";

    private const string TableSettingsSql
        = "SELECT TABLE_NAME AS \"NAME\", TABLE_COLLATION, ENGINE, CREATE_OPTIONS FROM INFORMATION_SCHEMA.TABLES "
            + "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'";

    // A functional index part has no column name, as in IndexesSql.
    private const string IndexSettingsSql
        = "SELECT TABLE_NAME AS \"TABLE\", INDEX_NAME AS \"NAME\", COLUMN_NAME AS \"COLUMN\", SEQ_IN_INDEX AS \"POSITION\", SUB_PART, INDEX_TYPE "
            + "FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND COLUMN_NAME IS NOT NULL";

    // A character set or collation is the table's own where it differs from the database's; an ENGINE where it differs from
    // the server's default; the options it was created with (row_format=DYNAMIC ...) as they are, but for flags without a
    // value (partitioned).
    private static void TableSettings(DatabaseTable table, IReadOnlyDictionary<string, object?> row, IReadOnlyDictionary<string, object?> defaults)
    {
        if (Text(row, "TABLE_COLLATION") is { } collation)
        {
            var charSet = collation.Split('_')[0];
            if (charSet != Text(defaults, "CHARSET"))
            {
                table[MySqlModelOptionsExtensions.CharSetAnnotation] = charSet;
            }

            if (collation != Text(defaults, "COLLATION"))
            {
                table[MySqlModelOptionsExtensions.CollationAnnotation] = collation;
            }
        }

        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if ((Text(row, "ENGINE") is { Length: > 0 } engine) && !string.Equals(engine, Text(defaults, "ENGINE"), StringComparison.OrdinalIgnoreCase))
        {
            MySqlTableOptions.Update(options, "ENGINE", engine);
        }

        foreach (Match option in CreateOption().Matches(Text(row, "CREATE_OPTIONS") ?? ""))
        {
            MySqlTableOptions.Update(options, option.Groups["name"].Value, option.Groups["value"].Value);
        }

        table[MySqlModelOptionsExtensions.StoreOptionsAnnotation] = MySqlTableOptions.Serialize(options);
    }

    // A FULLTEXT or SPATIAL index is marked so; a prefix length is the index's or the primary key's, but a SPATIAL index's
    // SUB_PART is the server's own (32 bytes), not one the model gave.
    private static void IndexSettings(DatabaseTable table, string name, List<IReadOnlyDictionary<string, object?>> rows)
    {
        Annotatable? target = name == "PRIMARY" ? table.PrimaryKey : table.Indexes.FirstOrDefault(i => i.Name == name);
        if (target == null)
        {
            return;
        }

        var type = Text(rows[0], "INDEX_TYPE");
        if (string.Equals(type, "FULLTEXT", StringComparison.OrdinalIgnoreCase))
        {
            target[MySqlFullTextExtensions.FullTextAnnotation] = true;
        }

        if (string.Equals(type, "SPATIAL", StringComparison.OrdinalIgnoreCase))
        {
            target[MySqlModelOptionsExtensions.SpatialAnnotation] = true;
            return;
        }

        var prefixLengths = rows.Select(r => r["SUB_PART"] is { } part ? Convert.ToInt32(part, CultureInfo.InvariantCulture) : 0).ToArray();
        if (prefixLengths.Any(p => p > 0))
        {
            target[MySqlModelOptionsExtensions.PrefixLengthAnnotation] = prefixLengths;
        }
    }

    // The catalog doesn't hold a FULLTEXT index's parser; SHOW CREATE TABLE writes it, in a version comment, after the
    // index's columns. Behaviour reference (not copied): Pomelo's GetFullTextParsers, at the reference above.
    private static void FullTextParsers(DbConnection connection, DatabaseTable table)
    {
        var create = Query(connection, "SHOW CREATE TABLE \"" + table.Name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"")
            .Select(r => Text(r, "Create Table")).FirstOrDefault() ?? "";
        foreach (Match parser in FullTextParser().Matches(create))
        {
            var indexName = parser.Groups["index"].Value.Replace("\"\"", "\"", StringComparison.Ordinal).Replace("``", "`", StringComparison.Ordinal);
            if (table.Indexes.FirstOrDefault(i => i.Name == indexName) is { } index)
            {
                index[MySqlFullTextExtensions.FullTextParserAnnotation] = parser.Groups["parser"].Value;
            }
        }
    }

    private static List<IReadOnlyDictionary<string, object?>> Query(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    // name=value in CREATE_OPTIONS (MariaDB quotes some names); a value is a word or a quoted string with nothing that could
    // end a statement.
    [GeneratedRegex(@"[`""]?(?<name>[A-Za-z_][A-Za-z0-9_]*)[`""]?=(?<value>'[^';#\r\n]*'|[A-Za-z0-9_.]+)")]
    private static partial Regex CreateOption();

    // FULLTEXT KEY "name" (...) /*!50100 WITH PARSER "ngram" */, under ANSI_QUOTES or not.
    [GeneratedRegex(@"FULLTEXT KEY [`""](?<index>(?:[^`""]|``|"""")+)[`""][^\n]*?WITH PARSER [`""]?(?<parser>[A-Za-z0-9_]+)")]
    private static partial Regex FullTextParser();

    private static ValueGenerated? GeneratedValue(string extra)
    {
        if (extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase))
        {
            return ValueGenerated.OnAdd;
        }

        return extra.Contains("on update", StringComparison.OrdinalIgnoreCase)
            ? ValueGenerated.OnAddOrUpdate
            : null;
    }

    // Upper case, as the dialect writes types, outside the quoted values of an ENUM or SET.
    // MySQL dropped integer display widths in 8.0.19 but for TINYINT(1) and ZEROFILL, where they still mean something to a
    // client; MariaDB reports them all (BIGINT(20)), and they don't change the values.
    protected override string StoreType(IReadOnlyDictionary<string, object?> row)
    {
        var type = QuotedOrNot().Replace(Text(row, "COLUMN_TYPE")!, m => m.Value.StartsWith('\'') ? m.Value : m.Value.ToUpperInvariant());
        return (type.StartsWith("TINYINT(1)", StringComparison.Ordinal) || type.Contains("ZEROFILL", StringComparison.Ordinal))
            ? type
            : IntegerDisplayWidth().Replace(type, "$1");
    }

    // COLUMN_DEFAULT is the bare value; an expression (DEFAULT_GENERATED) or CURRENT_TIMESTAMP is SQL already, and a bit
    // value keeps its b'...' form.
    protected override string? DefaultValueSql(IReadOnlyDictionary<string, object?> row)
    {
        if (Text(row, "DEFAULT") is not { } value)
        {
            return null;
        }

        var dataType = Text(row, "DATA_TYPE") ?? "";
        var extra = Text(row, "EXTRA") ?? "";
        if ((extra.Contains("DEFAULT_GENERATED", StringComparison.OrdinalIgnoreCase) && !SimpleNumber().IsMatch(value))
            || CurrentTimestamp().IsMatch(value)
            || ((dataType == "bit") && value.StartsWith("b'", StringComparison.Ordinal)))
        {
            return value;
        }

        return "'" + value.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    // RESTRICT is NO ACTION in MySQL.
    protected override ReferentialAction? DeleteAction(string? rule)
        => string.Equals(rule, "RESTRICT", StringComparison.OrdinalIgnoreCase) ? ReferentialAction.NoAction : base.DeleteAction(rule);

    [GeneratedRegex(@"'(?:[^']|'')*'|[^']+")]
    private static partial Regex QuotedOrNot();

    [GeneratedRegex(@"^(TINYINT|SMALLINT|MEDIUMINT|INT|BIGINT)\(\d+\)")]
    private static partial Regex IntegerDisplayWidth();

    [GeneratedRegex(@"^\((.*)\)$", RegexOptions.Singleline)]
    private static partial Regex OuterParentheses();

    [GeneratedRegex(@"^\d+(?:\.\d+)?$")]
    private static partial Regex SimpleNumber();

    [GeneratedRegex(@"^CURRENT_TIMESTAMP(?:\(\d*\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex CurrentTimestamp();
}

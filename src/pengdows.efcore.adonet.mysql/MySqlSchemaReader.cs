// Portions adapted from Pomelo's MySqlDatabaseModelFactory.
// Copyright (c) Pomelo Foundation. Licensed under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL's catalog: the database is the schema; a column's full type is <c>COLUMN_TYPE</c>, its generation and
///     <c>AUTO_INCREMENT</c> are in <c>EXTRA</c>, and its default is the bare value unless the server marks it an expression;
///     indexes are in <c>STATISTICS</c>, where unique constraints also appear (so only the primary key is read as a key); a
///     foreign key names its principal in <c>KEY_COLUMN_USAGE</c>; there are no sequences. Adapted from Pomelo's
///     MySqlDatabaseModelFactory (MIT, Pomelo Foundation):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Scaffolding/Internal/MySqlDatabaseModelFactory.cs
/// </summary>
internal sealed partial class MySqlSchemaReader : InformationSchemaReader
{
    public static readonly MySqlSchemaReader Instance = new();

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
            + "c.GENERATION_EXPRESSION, c.COLLATION_NAME, t.TABLE_COLLATION "
            + "FROM INFORMATION_SCHEMA.COLUMNS AS c JOIN INFORMATION_SCHEMA.TABLES AS t "
            + "ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME WHERE c.TABLE_SCHEMA = {schema}";

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

        column.ValueGenerated = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase)
            ? ValueGenerated.OnAdd
            : extra.Contains("on update", StringComparison.OrdinalIgnoreCase)
                ? ValueGenerated.OnAddOrUpdate
                : null;
        column.Comment = Text(row, "COLUMN_COMMENT") is { Length: > 0 } comment ? comment : null;

        // A collation is the column's own only where it differs from its table's.
        column.Collation = Text(row, "COLLATION_NAME") is { } collation && (collation != Text(row, "TABLE_COLLATION")) ? collation : null;
        return column;
    }

    // Upper case, as the dialect writes types, outside the quoted values of an ENUM or SET.
    protected override string StoreType(IReadOnlyDictionary<string, object?> row)
        => QuotedOrNot().Replace(Text(row, "COLUMN_TYPE")!, m => m.Value.StartsWith('\'') ? m.Value : m.Value.ToUpperInvariant());

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

    [GeneratedRegex(@"^\((.*)\)$", RegexOptions.Singleline)]
    private static partial Regex OuterParentheses();

    [GeneratedRegex(@"^\d+(?:\.\d+)?$")]
    private static partial Regex SimpleNumber();

    [GeneratedRegex(@"^CURRENT_TIMESTAMP(?:\(\d*\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex CurrentTimestamp();
}

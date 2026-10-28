using System.Globalization;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.sqlite;

/// <summary>
///     SQLite catalog reader based on sqlite_master and the PRAGMA table-valued functions, reading what Microsoft's SQLite
///     model factory reads: AUTOINCREMENT keys, generated columns, unique constraints, descending and partial indexes, and
///     foreign keys naming no column (the principal's key). It reads no SpatiaLite or migrations bookkeeping table.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Scaffolding/Internal/SqliteDatabaseModelFactory.cs
/// </summary>
internal sealed class SqliteSchemaReader : SchemaReader
{
    public static readonly SqliteSchemaReader Instance = new();

    // Tables SpatiaLite's InitSpatialMetaData creates, and the migrations history and lock tables.
    private const string Bookkeeping =
        "'__EFMigrationsHistory', '__EFMigrationsLock', 'ElementaryGeometries', 'geometry_columns', 'geometry_columns_auth', "
        + "'geometry_columns_field_infos', 'geometry_columns_statistics', 'geometry_columns_time', 'spatial_ref_sys', "
        + "'spatial_ref_sys_aux', 'SpatialIndex', 'spatialite_history', 'sql_statements_log', 'vector_layers', "
        + "'vector_layers_auth', 'vector_layers_statistics', 'vector_layers_field_infos', 'views_geometry_columns', "
        + "'views_geometry_columns_auth', 'views_geometry_columns_field_infos', 'views_geometry_columns_statistics', "
        + "'virts_geometry_columns', 'virts_geometry_columns_auth', 'geom_cols_ref_sys', 'spatial_ref_sys_all', "
        + "'virts_geometry_columns_field_infos', 'virts_geometry_columns_statistics'";

    private static readonly Regex Autoincrement = new(@"\bAUTOINCREMENT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public override bool ScaffoldsSchemas => false;
    public override bool KeepsConstraintNames => false;
    public override bool ReadsIndexFilters => true;

    public override DatabaseModel Read(DbConnection connection, DatabaseModelFactoryOptions options, ISqlDialect sql)
    {
        var model = new DatabaseModel { DatabaseName = connection.Database, DefaultSchema = null };
        var wanted = options.Tables.ToHashSet(StringComparer.Ordinal);
        var tables = new Dictionary<string, DatabaseTable>(StringComparer.Ordinal);
        var autoincrement = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in Rows(connection,
                     "SELECT \"name\", \"type\", \"sql\" FROM \"sqlite_master\" WHERE \"type\" IN ('table', 'view') "
                     + "AND instr(\"name\", 'sqlite_') <> 1 AND \"name\" NOT IN (" + Bookkeeping + ") ORDER BY \"name\""))
        {
            var name = Text(row, "name")!;
            if ((wanted.Count > 0) && !wanted.Contains(name))
            {
                continue;
            }

            DatabaseTable table = string.Equals(Text(row, "type"), "view", StringComparison.OrdinalIgnoreCase)
                ? new DatabaseView()
                : new DatabaseTable();
            table.Database = model;
            table.Name = name;
            model.Tables.Add(table);
            tables.Add(name, table);
            if ((table is not DatabaseView) && Autoincrement.IsMatch(WithoutQuotedText(Text(row, "sql") ?? "")))
            {
                autoincrement.Add(name);
            }
        }

        var indexSql = Rows(connection, "SELECT \"name\", \"sql\" FROM \"sqlite_master\" WHERE \"type\" = 'index'")
            .Where(r => Text(r, "sql") != null)
            .ToDictionary(r => Text(r, "name")!, r => Text(r, "sql")!, StringComparer.Ordinal);

        foreach (var table in tables.Values)
        {
            ReadColumnsAndKey(connection, table, autoincrement.Contains(table.Name));
            if (table is not DatabaseView)
            {
                ReadIndexes(connection, table, indexSql);
            }
        }

        // Foreign keys last: one naming no column references its principal's key, which may be read after it.
        foreach (var table in tables.Values.Where(t => t is not DatabaseView))
        {
            ReadForeignKeys(connection, table, tables);
        }

        return model;
    }

    // hidden: 0 an ordinary column, 1 a virtual table's hidden one, 2 generated (virtual), 3 generated (stored).
    private static void ReadColumnsAndKey(DbConnection connection, DatabaseTable table, bool autoincrement)
    {
        var keyColumns = new List<(long Position, DatabaseColumn Column)>();
        foreach (var row in Rows(connection, $"PRAGMA table_xinfo({Quote(table.Name)})"))
        {
            var hidden = Number(row, "hidden");
            if (hidden == 1)
            {
                continue;
            }

            var column = new DatabaseColumn
            {
                Table = table,
                Name = Text(row, "name")!,
                StoreType = Text(row, "type") is { Length: > 0 } type ? type : "TEXT",
                IsNullable = Number(row, "notnull") == 0,
                DefaultValueSql = Text(row, "dflt_value"),
                ComputedColumnSql = hidden is 2 or 3 ? string.Empty : null,
                IsStored = hidden == 3 ? true : null
            };
            table.Columns.Add(column);
            if (Number(row, "pk") is > 0 and var position)
            {
                keyColumns.Add((position, column));
            }
        }

        if (keyColumns.Count == 0)
        {
            return;
        }

        var key = new DatabasePrimaryKey { Table = table, Name = $"PK_{table.Name}" };
        foreach (var (_, column) in keyColumns.OrderBy(k => k.Position))
        {
            key.Columns.Add(column);
        }

        table.PrimaryKey = key;

        // Only an INTEGER PRIMARY KEY can be AUTOINCREMENT; a plain rowid key is generated too, but by convention.
        if (autoincrement && (keyColumns.Count == 1) && string.Equals(keyColumns[0].Column.StoreType, "INTEGER", StringComparison.OrdinalIgnoreCase))
        {
            keyColumns[0].Column.ValueGenerated = ValueGenerated.OnAdd;
        }
    }

    // origin: 'pk' the primary key (read from the columns), 'u' a UNIQUE constraint, 'c' a CREATE INDEX.
    private static void ReadIndexes(DbConnection connection, DatabaseTable table, Dictionary<string, string> indexSql)
    {
        foreach (var indexRow in Rows(connection, $"PRAGMA index_list({Quote(table.Name)})"))
        {
            var origin = Text(indexRow, "origin");
            if (string.Equals(origin, "pk", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Text(indexRow, "name")!;
            var keys = Rows(connection, $"PRAGMA index_xinfo({Quote(name)})")
                .Where(r => Number(r, "key") == 1)
                .OrderBy(r => Number(r, "seqno"))
                .Select(r => (Column: table.Columns.FirstOrDefault(c => c.Name == Text(r, "name")), Descending: Number(r, "desc") != 0))
                .ToList();
            if (keys.Any(k => k.Column == null))
            {
                // An index on an expression has no column to scaffold.
                continue;
            }

            if (string.Equals(origin, "u", StringComparison.OrdinalIgnoreCase))
            {
                var unique = new DatabaseUniqueConstraint { Table = table, Name = name.StartsWith("sqlite_", StringComparison.Ordinal) ? "" : name };
                foreach (var (column, _) in keys)
                {
                    unique.Columns.Add(column!);
                }

                table.UniqueConstraints.Add(unique);
                continue;
            }

            var index = new DatabaseIndex
            {
                Table = table,
                Name = name,
                IsUnique = Number(indexRow, "unique") != 0,
                Filter = (Number(indexRow, "partial") != 0) && indexSql.TryGetValue(name, out var sql) ? IndexFilter(sql) : null
            };
            foreach (var (column, descending) in keys)
            {
                index.Columns.Add(column!);
                index.IsDescending.Add(descending);
            }

            if (index.Columns.Count > 0)
            {
                table.Indexes.Add(index);
            }
        }
    }

    private void ReadForeignKeys(DbConnection connection, DatabaseTable table, Dictionary<string, DatabaseTable> tables)
    {
        foreach (var group in Rows(connection, $"PRAGMA foreign_key_list({Quote(table.Name)})").GroupBy(r => Number(r, "id")))
        {
            var first = group.First();
            if (!tables.TryGetValue(Text(first, "table")!, out var principal))
            {
                continue;
            }

            var foreignKey = new DatabaseForeignKey
            {
                Table = table,
                PrincipalTable = principal,
                Name = $"FK_{table.Name}_{group.Key}",
                OnDelete = DeleteAction(Text(first, "on_delete"))
            };
            foreach (var row in group.OrderBy(r => Number(r, "seq")))
            {
                var dependent = table.Columns.FirstOrDefault(c => c.Name == Text(row, "from"));
                var position = (int)Number(row, "seq");
                // REFERENCES "Blogs" with no column list is the principal's key.
                var principalColumn = Text(row, "to") is { } to
                    ? principal.Columns.FirstOrDefault(c => c.Name == to)
                    : (principal.PrimaryKey is { } key) && (position < key.Columns.Count) ? key.Columns[position] : null;
                if ((dependent == null) || (principalColumn == null))
                {
                    foreignKey.Columns.Clear();
                    break;
                }

                foreignKey.Columns.Add(dependent);
                foreignKey.PrincipalColumns.Add(principalColumn);
            }

            if (foreignKey.Columns.Count > 0)
            {
                table.ForeignKeys.Add(foreignKey);
            }
        }
    }

    /// <summary>The condition of a partial index's <c>CREATE INDEX ... (columns) WHERE condition</c>, or null.</summary>
    internal static string? IndexFilter(string createIndexSql)
    {
        var plain = WithoutQuotedText(createIndexSql);
        var open = plain.IndexOf('(');
        if (open < 0)
        {
            return null;
        }

        var depth = 0;
        for (var i = open; i < plain.Length; i++)
        {
            if (plain[i] == '(')
            {
                depth++;
                continue;
            }

            if (plain[i] != ')')
            {
                continue;
            }

            depth--;
            if (depth > 0)
            {
                continue;
            }

            var rest = plain[(i + 1)..];
            var where = Regex.Match(rest, @"^\s*WHERE\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return where.Success ? createIndexSql[(i + 1 + where.Length)..].Trim() : null;
        }

        return null;
    }

    // The text with every quoted run ('...', "...", [...], `...`) blanked to spaces, keeping each character's position.
    private static string WithoutQuotedText(string sql)
    {
        var text = new StringBuilder(sql);
        char? close = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (close == null)
            {
                close = c switch
                {
                    '\'' => '\'',
                    '"' => '"',
                    '`' => '`',
                    '[' => ']',
                    _ => null
                };
                continue;
            }

            if (c == close)
            {
                close = null;
                continue;
            }

            text[i] = ' ';
        }

        return text.ToString();
    }

    private static string Quote(string name) => '"' + name.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    private static long Number(Dictionary<string, object?> row, string column)
        => row.TryGetValue(column, out var value) && (value != null) ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : 0;

    private static List<Dictionary<string, object?>> Rows(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
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

    protected override ReferentialAction? DeleteAction(string? action)
        => action?.ToUpperInvariant() switch
        {
            "CASCADE" => ReferentialAction.Cascade,
            "SET NULL" => ReferentialAction.SetNull,
            "SET DEFAULT" => ReferentialAction.SetDefault,
            "RESTRICT" => ReferentialAction.Restrict,
            "NO ACTION" => ReferentialAction.NoAction,
            _ => null
        };
}

using System.Globalization;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.sqlite;

/// <summary>SQLite catalog reader based on sqlite_master and the PRAGMA table-valued functions.</summary>
internal sealed class SqliteSchemaReader : SchemaReader
{
    public static readonly SqliteSchemaReader Instance = new();

    public override bool ScaffoldsSchemas => false;
    public override bool KeepsConstraintNames => false;
    public override bool ReadsIndexFilters => true;

    public override DatabaseModel Read(DbConnection connection, DatabaseModelFactoryOptions options, ISqlDialect sql)
    {
        var model = new DatabaseModel { DatabaseName = connection.Database, DefaultSchema = null };
        var wanted = options.Tables.ToHashSet(StringComparer.Ordinal);
        var tables = new Dictionary<string, DatabaseTable>(StringComparer.Ordinal);

        foreach (var row in Rows(connection,
                     "SELECT name, type, sql FROM sqlite_master WHERE type IN ('table','view') AND name NOT LIKE 'sqlite_%' ORDER BY name"))
        {
            var name = Text(row, "name")!;
            if (wanted.Count > 0 && !wanted.Contains(name)) continue;
            DatabaseTable table = string.Equals(Text(row, "type"), "view", StringComparison.OrdinalIgnoreCase)
                ? new DatabaseView()
                : new DatabaseTable();
            table.Database = model;
            table.Name = name;
            table.Comment = null;
            model.Tables.Add(table);
            tables.Add(name, table);
        }

        foreach (var table in tables.Values.OfType<DatabaseTable>())
        {
            foreach (var row in Rows(connection, $"PRAGMA table_info({Quote(table.Name)})"))
            {
                var column = new DatabaseColumn
                {
                    Table = table,
                    Name = Text(row, "name")!,
                    StoreType = Text(row, "type") is { Length: > 0 } type ? type : "TEXT",
                    IsNullable = Convert.ToInt64(row["notnull"] ?? 0, CultureInfo.InvariantCulture) == 0,
                    DefaultValueSql = Text(row, "dflt_value"),
                    ValueGenerated = Convert.ToInt64(row["pk"] ?? 0, CultureInfo.InvariantCulture) == 1
                        ? ValueGenerated.OnAdd
                        : null
                };
                table.Columns.Add(column);
            }

            var pkColumns = table.Columns
                .Select((column, index) => (column, index))
                .Where(x => Convert.ToInt64(Rows(connection, $"PRAGMA table_info({Quote(table.Name)})")[x.index]["pk"] ?? 0, CultureInfo.InvariantCulture) > 0)
                .OrderBy(x => x.index)
                .Select(x => x.column)
                .ToList();
            if (pkColumns.Count > 0)
            {
                var key = new DatabasePrimaryKey { Table = table, Name = $"PK_{table.Name}" };
                foreach (var column in pkColumns) key.Columns.Add(column);
                table.PrimaryKey = key;
            }

            foreach (var indexRow in Rows(connection, $"PRAGMA index_list({Quote(table.Name)})"))
            {
                var name = Text(indexRow, "name")!;
                if (string.Equals(Text(indexRow, "origin"), "pk", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Text(indexRow, "origin"), "u", StringComparison.OrdinalIgnoreCase)) continue;
                var index = new DatabaseIndex
                {
                    Table = table,
                    Name = name,
                    IsUnique = Convert.ToInt64(indexRow["unique"] ?? 0, CultureInfo.InvariantCulture) != 0,
                    Filter = Convert.ToInt64(indexRow["partial"] ?? 0, CultureInfo.InvariantCulture) != 0
                        ? ScalarText(connection, $"SELECT sql FROM sqlite_master WHERE name = {Literal(name)}")
                        : null
                };
                foreach (var indexColumn in Rows(connection, $"PRAGMA index_info({Quote(name)})").OrderBy(r => Convert.ToInt64(r["seqno"], CultureInfo.InvariantCulture)))
                {
                    var column = table.Columns.FirstOrDefault(c => c.Name == Text(indexColumn, "name"));
                    if (column != null) index.Columns.Add(column);
                }
                if (index.Columns.Count > 0) table.Indexes.Add(index);
            }

            foreach (var fkGroup in Rows(connection, $"PRAGMA foreign_key_list({Quote(table.Name)})")
                         .GroupBy(r => Convert.ToInt64(r["id"], CultureInfo.InvariantCulture)))
            {
                var first = fkGroup.First();
                if (!tables.TryGetValue(Text(first, "table")!, out var principal)) continue;
                var foreignKey = new DatabaseForeignKey
                {
                    Table = table,
                    PrincipalTable = principal,
                    Name = $"FK_{table.Name}_{fkGroup.Key}",
                    OnDelete = DeleteAction(Text(first, "on_delete"))
                };
                foreach (var fk in fkGroup.OrderBy(r => Convert.ToInt64(r["seq"], CultureInfo.InvariantCulture)))
                {
                    var dependent = table.Columns.FirstOrDefault(c => c.Name == Text(fk, "from"));
                    var principalColumn = principal.Columns.FirstOrDefault(c => c.Name == Text(fk, "to"));
                    if (dependent != null && principalColumn != null)
                    {
                        foreignKey.Columns.Add(dependent);
                        foreignKey.PrincipalColumns.Add(principalColumn);
                    }
                }
                if (foreignKey.Columns.Count > 0) table.ForeignKeys.Add(foreignKey);
            }
        }

        return model;
    }

    private static string Quote(string name) => '"' + name.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    private static string? ScalarText(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() as string;
    }
    private static List<Dictionary<string, object?>> Rows(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
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

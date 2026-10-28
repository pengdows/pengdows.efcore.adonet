using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace pengdows.efcore.adonet.Scaffolding;

/// <summary>
///     Reads a database's schema from ISO's <c>INFORMATION_SCHEMA</c>. Each part comes from one query of flat rows, ISO's by default,
///     which a database overrides where its catalog differs; the rows are assembled here. <c>{schema}</c> in a query is the
///     dialect's <see cref="ISqlDialect.CurrentSchema" />.
/// </summary>
/// <remarks>
///     Row shapes, by column alias:
///     tables <c>SCHEMA, NAME, TYPE ('BASE TABLE' or 'VIEW'), COMMENT</c>;
///     columns <c>SCHEMA, TABLE, NAME, POSITION, NULLABLE ('YES'/'NO'), DEFAULT</c> and what <see cref="StoreType" />,
///     <see cref="DefaultValueSql" /> and <see cref="Column" /> read;
///     keys <c>SCHEMA, TABLE, NAME, TYPE ('PRIMARY KEY' or 'UNIQUE'), COLUMN, POSITION</c>;
///     foreign keys <c>SCHEMA, TABLE, NAME, COLUMN, POSITION, PRINCIPAL_SCHEMA, PRINCIPAL_TABLE, PRINCIPAL_COLUMN, DELETE_RULE</c>;
///     indexes <c>SCHEMA, TABLE, NAME, IS_UNIQUE, COLUMN, POSITION, IS_DESCENDING, FILTER</c>;
///     sequences <c>SCHEMA, NAME, DATA_TYPE, START, INCREMENT, MIN, MAX, CYCLE</c>.
/// </remarks>
public class InformationSchemaReader : SchemaReader
{
    public static readonly InformationSchemaReader Iso = new();

    public virtual string TablesSql
        => "SELECT TABLE_SCHEMA AS \"SCHEMA\", TABLE_NAME AS \"NAME\", TABLE_TYPE AS \"TYPE\", NULL AS \"COMMENT\" "
            + "FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = {schema} AND TABLE_TYPE IN ('BASE TABLE', 'VIEW')";

    public virtual string ColumnsSql
        => "SELECT TABLE_SCHEMA AS \"SCHEMA\", TABLE_NAME AS \"TABLE\", COLUMN_NAME AS \"NAME\", ORDINAL_POSITION AS \"POSITION\", "
            + "IS_NULLABLE AS \"NULLABLE\", COLUMN_DEFAULT AS \"DEFAULT\", DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, "
            + "NUMERIC_SCALE, DATETIME_PRECISION, COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = {schema}";

    public virtual string KeysSql
        => "SELECT k.TABLE_SCHEMA AS \"SCHEMA\", k.TABLE_NAME AS \"TABLE\", k.CONSTRAINT_NAME AS \"NAME\", "
            + "c.CONSTRAINT_TYPE AS \"TYPE\", k.COLUMN_NAME AS \"COLUMN\", k.ORDINAL_POSITION AS \"POSITION\" "
            + "FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS c JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS k "
            + "ON k.CONSTRAINT_SCHEMA = c.CONSTRAINT_SCHEMA AND k.CONSTRAINT_NAME = c.CONSTRAINT_NAME AND k.TABLE_NAME = c.TABLE_NAME "
            + "WHERE c.TABLE_SCHEMA = {schema} AND c.CONSTRAINT_TYPE IN ('PRIMARY KEY', 'UNIQUE')";

    // The principal's columns through the unique constraint the foreign key references, position by position.
    public virtual string ForeignKeysSql
        => "SELECT k.TABLE_SCHEMA AS \"SCHEMA\", k.TABLE_NAME AS \"TABLE\", k.CONSTRAINT_NAME AS \"NAME\", k.COLUMN_NAME AS \"COLUMN\", "
            + "k.ORDINAL_POSITION AS \"POSITION\", p.TABLE_SCHEMA AS \"PRINCIPAL_SCHEMA\", p.TABLE_NAME AS \"PRINCIPAL_TABLE\", "
            + "p.COLUMN_NAME AS \"PRINCIPAL_COLUMN\", r.DELETE_RULE AS \"DELETE_RULE\" "
            + "FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS AS r "
            + "JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS k ON k.CONSTRAINT_SCHEMA = r.CONSTRAINT_SCHEMA AND k.CONSTRAINT_NAME = r.CONSTRAINT_NAME "
            + "JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS p ON p.CONSTRAINT_SCHEMA = r.UNIQUE_CONSTRAINT_SCHEMA "
            + "AND p.CONSTRAINT_NAME = r.UNIQUE_CONSTRAINT_NAME AND p.ORDINAL_POSITION = k.POSITION_IN_UNIQUE_CONSTRAINT "
            + "WHERE r.CONSTRAINT_SCHEMA = {schema}";

    /// <summary>Indexes, which ISO's catalog doesn't describe; null where the database can't say.</summary>
    public virtual string? IndexesSql
        => null;

    public virtual string? SequencesSql
        => "SELECT SEQUENCE_SCHEMA AS \"SCHEMA\", SEQUENCE_NAME AS \"NAME\", DATA_TYPE, START_VALUE AS \"START\", "
            + "INCREMENT AS \"INCREMENT\", MINIMUM_VALUE AS \"MIN\", MAXIMUM_VALUE AS \"MAX\", CYCLE_OPTION AS \"CYCLE\" "
            + "FROM INFORMATION_SCHEMA.SEQUENCES WHERE SEQUENCE_SCHEMA = {schema}";

    public override DatabaseModel Read(DbConnection connection, DatabaseModelFactoryOptions options, ISqlDialect sql)
    {
        var schema = sql.CurrentSchema
            ?? throw new NotSupportedException("Reading a schema needs ISqlDialect.CurrentSchema, the schema to read.");
        var model = new DatabaseModel { DatabaseName = connection.Database };
        model.DefaultSchema = ScaffoldsSchemas ? Convert.ToString(Scalar(connection, "SELECT " + schema), CultureInfo.InvariantCulture) : null;

        var wanted = options.Tables.ToHashSet(StringComparer.Ordinal);
        var tables = new Dictionary<(string?, string), DatabaseTable>();
        foreach (var row in Rows(connection, TablesSql, schema))
        {
            var name = Text(row, "NAME")!;
            if ((wanted.Count > 0) && !wanted.Contains(name))
            {
                continue;
            }

            DatabaseTable table = string.Equals(Text(row, "TYPE"), "VIEW", StringComparison.OrdinalIgnoreCase) ? new DatabaseView() : new DatabaseTable();
            table.Database = model;
            table.Schema = SchemaOf(row, "SCHEMA");
            table.Name = name;
            table.Comment = Text(row, "COMMENT") is { Length: > 0 } comment ? comment : null;
            model.Tables.Add(table);
            tables[(table.Schema, name)] = table;
        }

        foreach (var row in Rows(connection, ColumnsSql, schema).OrderBy(r => Convert.ToInt64(r["POSITION"], CultureInfo.InvariantCulture)))
        {
            if (tables.TryGetValue((SchemaOf(row, "SCHEMA"), Text(row, "TABLE")!), out var table))
            {
                var column = Column(row);
                column.Table = table;
                table.Columns.Add(column);
            }
        }

        ReadKeys(connection, schema, tables);
        ReadForeignKeys(connection, schema, tables);
        ReadIndexes(connection, schema, tables);
        if (SequencesSql is { } sequencesSql)
        {
            foreach (var row in Rows(connection, sequencesSql, schema))
            {
                model.Sequences.Add(new DatabaseSequence
                {
                    Database = model,
                    Schema = SchemaOf(row, "SCHEMA"),
                    Name = Text(row, "NAME")!,
                    StoreType = Text(row, "DATA_TYPE"),
                    StartValue = Long(row, "START"),
                    IncrementBy = (int?)Long(row, "INCREMENT"),
                    MinValue = Long(row, "MIN"),
                    MaxValue = Long(row, "MAX"),
                    IsCyclic = row["CYCLE"] is var cycle and not null
                        && (Convert.ToString(cycle, CultureInfo.InvariantCulture) is "YES" or "1" or "True" or "true")
                });
            }
        }

        return model;
    }

    /// <summary>A column from its row.</summary>
    protected virtual DatabaseColumn Column(IReadOnlyDictionary<string, object?> row)
        => new()
        {
            Name = Text(row, "NAME")!,
            StoreType = StoreType(row),
            IsNullable = string.Equals(Text(row, "NULLABLE"), "YES", StringComparison.OrdinalIgnoreCase),
            DefaultValueSql = DefaultValueSql(row),
            Collation = row.TryGetValue("COLLATION_NAME", out var collation) ? collation as string : null
        };

    /// <summary>The column's store type: ISO's data type, with its length or precision and scale.</summary>
    protected virtual string StoreType(IReadOnlyDictionary<string, object?> row)
    {
        var type = Text(row, "DATA_TYPE")!;
        if (Long(row, "CHARACTER_MAXIMUM_LENGTH") is { } length and > 0)
        {
            return $"{type}({length.ToString(CultureInfo.InvariantCulture)})";
        }

        if (type.Contains("DECIMAL", StringComparison.OrdinalIgnoreCase) || type.Contains("NUMERIC", StringComparison.OrdinalIgnoreCase))
        {
            return Long(row, "NUMERIC_PRECISION") is { } precision
                ? $"{type}({precision.ToString(CultureInfo.InvariantCulture)},{(Long(row, "NUMERIC_SCALE") ?? 0).ToString(CultureInfo.InvariantCulture)})"
                : type;
        }

        return type;
    }

    /// <summary>The column's default as SQL; ISO's catalog already holds it so.</summary>
    protected virtual string? DefaultValueSql(IReadOnlyDictionary<string, object?> row)
        => Text(row, "DEFAULT");

    private void ReadKeys(DbConnection connection, string schema, Dictionary<(string?, string), DatabaseTable> tables)
    {
        foreach (var key in Rows(connection, KeysSql, schema)
                     .GroupBy(r => (Schema: SchemaOf(r, "SCHEMA"), Table: Text(r, "TABLE")!, Name: Text(r, "NAME")!, Type: Text(r, "TYPE")!)))
        {
            if (!tables.TryGetValue((key.Key.Schema, key.Key.Table), out var table))
            {
                continue;
            }

            var columns = OrderedColumns(table, key);
            if (string.Equals(key.Key.Type, "PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
            {
                var primaryKey = new DatabasePrimaryKey { Table = table, Name = key.Key.Name };
                foreach (var column in columns)
                {
                    primaryKey.Columns.Add(column);
                }

                table.PrimaryKey = primaryKey;
            }
            else
            {
                var unique = new DatabaseUniqueConstraint { Table = table, Name = key.Key.Name };
                foreach (var column in columns)
                {
                    unique.Columns.Add(column);
                }

                table.UniqueConstraints.Add(unique);
            }
        }
    }

    private void ReadForeignKeys(DbConnection connection, string schema, Dictionary<(string?, string), DatabaseTable> tables)
    {
        foreach (var key in Rows(connection, ForeignKeysSql, schema)
                     .GroupBy(r => (Schema: SchemaOf(r, "SCHEMA"), Table: Text(r, "TABLE")!, Name: Text(r, "NAME")!)))
        {
            var first = key.First();
            if (!tables.TryGetValue((key.Key.Schema, key.Key.Table), out var table)
                || !tables.TryGetValue((SchemaOf(first, "PRINCIPAL_SCHEMA"), Text(first, "PRINCIPAL_TABLE")!), out var principal))
            {
                continue;
            }

            var foreignKey = new DatabaseForeignKey
            {
                Table = table, PrincipalTable = principal, Name = key.Key.Name, OnDelete = DeleteAction(Text(first, "DELETE_RULE"))
            };
            foreach (var row in key.OrderBy(r => Convert.ToInt64(r["POSITION"], CultureInfo.InvariantCulture)))
            {
                foreignKey.Columns.Add(Find(table, Text(row, "COLUMN")!));
                foreignKey.PrincipalColumns.Add(Find(principal, Text(row, "PRINCIPAL_COLUMN")!));
            }

            table.ForeignKeys.Add(foreignKey);
        }
    }

    private void ReadIndexes(DbConnection connection, string schema, Dictionary<(string?, string), DatabaseTable> tables)
    {
        if (IndexesSql is not { } indexesSql)
        {
            return;
        }

        foreach (var index in Rows(connection, indexesSql, schema)
                     .GroupBy(r => (Schema: SchemaOf(r, "SCHEMA"), Table: Text(r, "TABLE")!, Name: Text(r, "NAME")!)))
        {
            if (!tables.TryGetValue((index.Key.Schema, index.Key.Table), out var table))
            {
                continue;
            }

            var rows = index.OrderBy(r => Convert.ToInt64(r["POSITION"], CultureInfo.InvariantCulture)).ToList();
            var databaseIndex = new DatabaseIndex
            {
                Table = table,
                Name = index.Key.Name,
                IsUnique = Bool(rows[0], "IS_UNIQUE"),
                Filter = Text(rows[0], "FILTER"),
                IsDescending = rows.Select(r => Bool(r, "IS_DESCENDING")).ToArray()
            };
            foreach (var row in rows)
            {
                databaseIndex.Columns.Add(Find(table, Text(row, "COLUMN")!));
            }

            // All ascending is EF's default, written as none.
            if (databaseIndex.IsDescending.All(d => !d))
            {
                databaseIndex.IsDescending = [];
            }

            table.Indexes.Add(databaseIndex);
        }
    }

    private static List<DatabaseColumn> OrderedColumns(DatabaseTable table, IEnumerable<IReadOnlyDictionary<string, object?>> rows)
        => rows.OrderBy(r => Convert.ToInt64(r["POSITION"], CultureInfo.InvariantCulture)).Select(r => Find(table, Text(r, "COLUMN")!)).ToList();

    private static DatabaseColumn Find(DatabaseTable table, string name)
        => table.Columns.FirstOrDefault(c => c.Name == name)
            ?? table.Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The catalog names column '{name}' of '{table.Name}', which it doesn't list.");

    private string? SchemaOf(IReadOnlyDictionary<string, object?> row, string column)
        => ScaffoldsSchemas ? Text(row, column) : null;

    private static object? Scalar(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static List<IReadOnlyDictionary<string, object?>> Rows(DbConnection connection, string sql, string schema)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql.Replace("{schema}", schema, StringComparison.Ordinal);
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
}

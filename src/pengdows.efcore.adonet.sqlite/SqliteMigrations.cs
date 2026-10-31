using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.sqlite;

/// <summary>
///     SQLite's ALTER TABLE renames and adds columns, nothing more. A constraint added to a table created in the same
///     migration goes into its CREATE TABLE, since SQLite checks a foreign key's table only when the key is used; any other
///     change of a table's constraints or columns rebuilds the table from the migration's target model, keeping its rows.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Migrations/SqliteMigrationsSqlGenerator.cs
/// </summary>
internal class SqliteMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
        => base.Generate(Rewrite(operations, model), model, options);

    private List<MigrationOperation> Rewrite(IReadOnlyList<MigrationOperation> operations, IModel? model)
    {
        var created = operations.OfType<CreateTableOperation>().ToDictionary(c => (c.Name, c.Schema));
        var folded = new HashSet<MigrationOperation>(operations.Where(o => FoldIntoCreateTable(o, created)));
        var rebuilt = new Dictionary<(string Table, string? Schema), int>();
        for (var i = 0; i < operations.Count; i++)
        {
            if (!folded.Contains(operations[i]) && NeedsRebuild(operations[i]) is { } table)
            {
                rebuilt[table] = i;
            }
        }

        foreach (var table in rebuilt.Keys)
        {
            if (model == null)
            {
                var operation = operations.First(o => NeedsRebuild(o) == table);
                throw new NotSupportedException(
                    $"SQLite can't run a {operation.GetType().Name} on table '{table.Table}' except by rebuilding the table, "
                    + "which needs the migration's target model (its BuildTargetModel).");
            }

            // The last change rebuilding a table runs the rebuild, which every other change of its columns and indexes is
            // part of.
            for (var i = 0; i < operations.Count; i++)
            {
                if (PartOfRebuild(operations[i]) == table)
                {
                    rebuilt[table] = Math.Max(rebuilt[table], i);
                }
            }
        }

        var rewritten = new List<MigrationOperation>();
        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            if (folded.Contains(operation))
            {
                continue;
            }

            if ((PartOfRebuild(operation) is { } table) && rebuilt.TryGetValue(table, out var last))
            {
                if (i == last)
                {
                    rewritten.AddRange(Rebuild(table, operations, model!));
                }

                continue;
            }

            rewritten.Add(operation);
        }

        return rewritten;
    }

    // A constraint added to a table this migration creates is declared with it; the table holds no rows to check yet.
    private static bool FoldIntoCreateTable(
        MigrationOperation operation, Dictionary<(string Name, string? Schema), CreateTableOperation> created)
    {
        switch (operation)
        {
            case AddForeignKeyOperation key when created.TryGetValue((key.Table, key.Schema), out var create):
                create.ForeignKeys.Add(key);
                return true;
            case AddPrimaryKeyOperation key when created.TryGetValue((key.Table, key.Schema), out var create):
                create.PrimaryKey = key;
                return true;
            case AddUniqueConstraintOperation unique when created.TryGetValue((unique.Table, unique.Schema), out var create):
                create.UniqueConstraints.Add(unique);
                return true;
            case AddCheckConstraintOperation check when created.TryGetValue((check.Table, check.Schema), out var create):
                create.CheckConstraints.Add(check);
                return true;
            default:
                return false;
        }
    }

    // Changes ALTER TABLE can't make. A dropped column is SQLite's own DROP COLUMN (3.35), which refuses, loudly, a column
    // still in a key or constraint, unless its table is rebuilt anyway.
    private static (string Table, string? Schema)? NeedsRebuild(MigrationOperation operation)
        => operation switch
        {
            AddForeignKeyOperation o => (o.Table, o.Schema),
            DropForeignKeyOperation o => (o.Table, o.Schema),
            AddPrimaryKeyOperation o => (o.Table, o.Schema),
            DropPrimaryKeyOperation o => (o.Table, o.Schema),
            AddUniqueConstraintOperation o => (o.Table, o.Schema),
            DropUniqueConstraintOperation o => (o.Table, o.Schema),
            AddCheckConstraintOperation o => (o.Table, o.Schema),
            DropCheckConstraintOperation o => (o.Table, o.Schema),
            AlterColumnOperation o => (o.Table, o.Schema),
            _ => null
        };

    // Changes a rebuild makes too, as the target model has them.
    private static (string Table, string? Schema)? PartOfRebuild(MigrationOperation operation)
        => NeedsRebuild(operation) ?? operation switch
        {
            AddColumnOperation o => (o.Table, o.Schema),
            DropColumnOperation o => (o.Table, o.Schema),
            RenameColumnOperation o => (o.Table, o.Schema),
            CreateIndexOperation o => (o.Table, o.Schema),
            DropIndexOperation { Table: { } t } o => (t, o.Schema),
            _ => null
        };

    // The table as the target model has it, created beside the old one, the old rows copied across (a renamed column from
    // its old name; a column this migration adds takes its default), the old table dropped and the new one renamed into
    // its place, then its indexes. Foreign keys are off meanwhile, so dropping the old table cascades nowhere; SQLite
    // ignores the pragma inside a transaction, so both run outside the migration's.
    private IEnumerable<MigrationOperation> Rebuild(
        (string Table, string? Schema) name, IReadOnlyList<MigrationOperation> operations, IModel model)
    {
        var table = model.GetRelationalModel().FindTable(name.Table, name.Schema)
            ?? throw new InvalidOperationException($"SQLite can't rebuild table '{name.Table}': the target model has no such table.");
        var temporary = "ef_temp_" + table.Name;
        var added = operations.OfType<AddColumnOperation>().Where(o => (o.Table, o.Schema) == name).Select(o => o.Name).ToHashSet();
        var renamed = operations.OfType<RenameColumnOperation>().Where(o => (o.Table, o.Schema) == name)
            .ToDictionary(o => o.NewName, o => o.Name);
        var copied = table.Columns.Where(c => !added.Contains(c.Name)).Select(c => c.Name).ToList();
        var helper = Dependencies.SqlGenerationHelper;

        yield return new SqlOperation { Sql = "PRAGMA foreign_keys = OFF;", SuppressTransaction = true };
        yield return CreateTable(table, temporary);
        yield return new SqlOperation
        {
            Sql = $"INSERT INTO {helper.DelimitIdentifier(temporary, table.Schema)} ({string.Join(", ", copied.Select(c => helper.DelimitIdentifier(c)))}) "
                + $"SELECT {string.Join(", ", copied.Select(c => helper.DelimitIdentifier(renamed.GetValueOrDefault(c, c))))} "
                + $"FROM {helper.DelimitIdentifier(table.Name, table.Schema)};"
        };
        yield return new DropTableOperation { Name = table.Name, Schema = table.Schema };
        yield return new RenameTableOperation { Name = temporary, Schema = table.Schema, NewName = table.Name, NewSchema = table.Schema };
        yield return new SqlOperation { Sql = "PRAGMA foreign_keys = ON;", SuppressTransaction = true };
        foreach (var index in table.Indexes)
        {
            yield return CreateIndexOperation.CreateFrom(index);
        }
    }

    private static CreateTableOperation CreateTable(ITable table, string name)
    {
        var create = new CreateTableOperation { Name = name, Schema = table.Schema, Comment = table.Comment };
        if (table.PrimaryKey != null)
        {
            create.PrimaryKey = AddPrimaryKeyOperation.CreateFrom(table.PrimaryKey);
            create.PrimaryKey.Table = name;
        }

        foreach (var column in table.Columns)
        {
            var add = new AddColumnOperation
            {
                Table = name,
                Schema = table.Schema,
                Name = column.Name,
                ClrType = column.ProviderClrType,
                ColumnType = column.StoreType,
                IsNullable = column.IsNullable,
                DefaultValue = column.DefaultValue,
                DefaultValueSql = column.DefaultValueSql,
                ComputedColumnSql = column.ComputedColumnSql,
                IsStored = column.IsStored,
                Collation = column.Collation,
                Comment = column.Comment
            };
            add.AddAnnotations(column.GetAnnotations());
            create.Columns.Add(add);
        }

        foreach (var key in table.ForeignKeyConstraints)
        {
            var add = AddForeignKeyOperation.CreateFrom(key);
            add.Table = name;
            create.ForeignKeys.Add(add);
        }

        foreach (var unique in table.UniqueConstraints.Where(c => !c.GetIsPrimaryKey()))
        {
            var add = AddUniqueConstraintOperation.CreateFrom(unique);
            add.Table = name;
            create.UniqueConstraints.Add(add);
        }

        foreach (var check in table.CheckConstraints)
        {
            var add = AddCheckConstraintOperation.CreateFrom(check);
            add.Table = name;
            create.CheckConstraints.Add(add);
        }

        return create;
    }

    private const string InlinePrimaryKey = "AdoNet:Sqlite:InlinePrimaryKey";

    // SQLite takes AUTOINCREMENT only on an INTEGER PRIMARY KEY column constraint, so a generated key of one column is written
    // there instead of as a table constraint. Without it the key is the plain rowid, and the next insert reuses the highest
    // key after its row is deleted. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Migrations/SqliteMigrationsSqlGenerator.cs
    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        // A SpatiaLite geometry column is declared by AddGeometryColumn after its table, which registers it in the spatial
        // catalog. Behaviour reference (not copied):
        // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Migrations/SqliteMigrationsSqlGenerator.cs
        var geometryColumns = operation.Columns.Where(c => IsGeometryColumn(c, model)).ToList();
        foreach (var column in geometryColumns)
        {
            operation.Columns.Remove(column);
        }

        GenerateTable(operation, model, builder, terminate || (geometryColumns.Count > 0));
        foreach (var column in geometryColumns)
        {
            AddGeometryColumn(column, model, builder);
        }
    }

    protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!IsGeometryColumn(operation, model))
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        AddGeometryColumn(operation, model, builder);
    }

    // SpatiaLite's catalog, made once, before the first geometry column.
    protected override void Generate(AlterDatabaseOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        base.Generate(operation, model, builder);
        if ((operation[SqliteAnnotationProvider.InitSpatialMetaData] is not true)
            || (operation.OldDatabase[SqliteAnnotationProvider.InitSpatialMetaData] is true))
        {
            return;
        }

        builder.Append("SELECT InitSpatialMetaData()").AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    private bool IsGeometryColumn(ColumnOperation operation, IModel? model)
        => (operation is AddColumnOperation add)
            && SqliteAnnotationProvider.IsGeometryType(add.ColumnType ?? GetColumnType(add.Schema, add.Table, add.Name, add, model));

    // AddGeometryColumn(table, column, srid, type, dimension, not null); a dimension of -1 takes it from the type's suffix.
    private void AddGeometryColumn(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        var text = Dependencies.TypeMappingSource.GetMapping(typeof(string));
        var number = Dependencies.TypeMappingSource.GetMapping(typeof(long));
        var type = operation.ColumnType ?? GetColumnType(operation.Schema, operation.Table, operation.Name, operation, model)!;
        builder
            .Append("SELECT AddGeometryColumn(")
            .Append(text.GenerateSqlLiteral(operation.Table))
            .Append(", ")
            .Append(text.GenerateSqlLiteral(operation.Name))
            .Append(", ")
            .Append(number.GenerateSqlLiteral((long)((operation[SqliteModelOptionsExtensions.SridAnnotation] ?? operation[SqliteModelOptionsExtensions.LegacySridAnnotation]) as int? ?? 0)))
            .Append(", ")
            .Append(text.GenerateSqlLiteral(type.ToUpperInvariant()))
            .Append(", -1, ")
            .Append(operation.IsNullable ? "0" : "1")
            .Append(")")
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    private void GenerateTable(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate)
    {
        if ((operation.PrimaryKey is { Columns: [var keyColumn] } key)
            && (operation.Columns.FirstOrDefault(c => c.Name == keyColumn) is { } column)
            && IsAutoincrementKey(operation, column, model))
        {
            column.AddAnnotation(InlinePrimaryKey, key.Name ?? string.Empty);
            operation.PrimaryKey = null;
        }

        base.Generate(operation, model, builder, terminate);
    }

    // AUTOINCREMENT is the strategy's, not the identity's: a key the database numbers is the plain rowid unless the model
    // says UseAutoincrement (Microsoft's default). Without a model (raw operations), the identity decides, as before.
    private static bool IsAutoincrementKey(CreateTableOperation operation, ColumnOperation column, IModel? model)
    {
        if (model is null)
        {
            return IsIdentity(column);
        }

        var property = model.GetEntityTypes()
            .Where(e => e.GetTableName() == operation.Name && e.GetSchema() == operation.Schema)
            .SelectMany(e => e.GetProperties())
            .FirstOrDefault(p => p.GetColumnName() == column.Name);

        return property is not null && property.GetValueGenerationStrategy() == SqliteValueGenerationStrategy.Autoincrement;
    }

    // A generated key of several columns has no AUTOINCREMENT in SQLite (ISqlDialect.GeneratesCompositeKeyValues).
    protected override void IdentityDefinition(string? schema, string table, string name, ColumnOperation operation, MigrationCommandListBuilder builder)
    {
        if (operation[InlinePrimaryKey] is not string keyName)
        {
            return;
        }

        if (keyName.Length > 0)
        {
            builder.Append(" CONSTRAINT ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(keyName));
        }

        builder.Append(" PRIMARY KEY AUTOINCREMENT");
    }
}

internal class SqliteHistoryRepository(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetHistoryRepository(dependencies, dialect)
{
    // The database file's lock: a sidecar file, locked exclusively while a migrator runs. An in-memory database is one process's and
    // needs no file; a file URI names no path this code can lock.
    protected override string? FileLockPath
    {
        get
        {
            var source = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(Dependencies.Connection.DbConnection.ConnectionString).DataSource;
            if (string.IsNullOrEmpty(source) || source == ":memory:" || source.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return source + ".migrations.lock";
        }
    }

    protected override string TableExistsSql(string table, string? schema)
        => "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" = '" + table.Replace("'", "''") + "'";
}

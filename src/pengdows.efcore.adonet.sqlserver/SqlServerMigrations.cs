using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     T-SQL where it differs from ISO: IDENTITY (with its seed and increment), sp_rename, ALTER COLUMN, named default
///     constraints, SPARSE columns, clustered and nonclustered keys and indexes with their options, and memory-optimized
///     tables. It reads Microsoft's annotation names, so migrations scaffolded by its provider keep their meaning.
/// </summary>
internal class SqlServerMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!terminate && operation.Comment is not null)
        {
            throw new ArgumentException("SQL Server cannot generate an unterminated ADD COLUMN statement with comments.", nameof(terminate));
        }

        base.Generate(operation, model, builder, terminate);
        if (operation.Comment is { Length: > 0 } comment)
        {
            AddDescription(builder, comment, operation.Schema, operation.Table, operation.Name);
        }
    }

    protected override void ForeignKeyAction(ReferentialAction referentialAction, MigrationCommandListBuilder builder)
    {
        // SQL Server has no RESTRICT action; NO ACTION is its equivalent and is the ISO spelling.
        if (referentialAction == ReferentialAction.Restrict)
        {
            builder.Append("NO ACTION");
            return;
        }

        base.ForeignKeyAction(referentialAction, builder);
    }

    // A computed column has no type of its own: name AS (expression), PERSISTED to store it, then any collation. Behaviour
    // reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Migrations/SqlServerMigrationsSqlGenerator.cs
    protected override void ComputedColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        builder
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name))
            .Append(" AS (")
            .Append(operation.ComputedColumnSql!)
            .Append(")");

        if (operation.IsStored == true)
        {
            builder.Append(" PERSISTED");
        }

        if (operation.Collation != null)
        {
            builder.Append(" COLLATE ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Collation));
        }
    }

    // As the core writes a column (name, type, default, identity, NOT NULL, collation), with SPARSE after the type and an
    // identity Microsoft's annotations name. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Migrations/SqlServerMigrationsSqlGenerator.cs
    protected override void ColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        if (operation.ComputedColumnSql != null)
        {
            ComputedColumnDefinition(schema, table, name, operation, model, builder);
            return;
        }

        var columnType = operation.ColumnType ?? GetColumnType(schema, table, name, operation, model)!;
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name)).Append(" ").Append(columnType);

        if (operation[SqlServerAnnotationNames.Sparse] is true)
        {
            builder.Append(" SPARSE");
        }

        if (operation[SqlServerAnnotationNames.TemporalIsPeriodStartColumn] is true)
        {
            builder.Append(" GENERATED ALWAYS AS ROW START HIDDEN");
        }

        if (operation[SqlServerAnnotationNames.TemporalIsPeriodEndColumn] is true)
        {
            builder.Append(" GENERATED ALWAYS AS ROW END HIDDEN");
        }

        DefaultValue(operation.DefaultValue, operation.DefaultValueSql, columnType, builder);

        if (IsIdentity(operation)
            || (operation[SqlServerAnnotationNames.Identity] is string)
            || (operation[SqlServerAnnotationNames.ValueGenerationStrategy] is SqlServerValueGenerationStrategy.IdentityColumn))
        {
            IdentityDefinition(schema, table, name, operation, builder);
        }

        if (!operation.IsNullable)
        {
            builder.Append(" NOT NULL");
        }

        if (operation.Collation != null)
        {
            builder.Append(" COLLATE ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Collation));
        }
    }

    // IDENTITY, or IDENTITY(seed, increment) where the annotation names other than "1, 1". The annotation is parsed, not
    // copied, so a migration can't put anything but two numbers there.
    protected override void IdentityDefinition(string? schema, string table, string name, ColumnOperation operation, MigrationCommandListBuilder builder)
    {
        builder.Append(" IDENTITY");
        if (operation[SqlServerAnnotationNames.Identity] is not string seedAndIncrement)
        {
            return;
        }

        var parts = seedAndIncrement.Split(',');
        if ((parts.Length != 2)
            || !long.TryParse(parts[0].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seed)
            || !int.TryParse(parts[1].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var increment))
        {
            throw new InvalidOperationException(
                $"'{table}.{name}' has the identity '{seedAndIncrement}', which is not a seed and an increment such as '1, 1'.");
        }

        if ((seed != 1) || (increment != 1))
        {
            builder.Append(string.Create(CultureInfo.InvariantCulture, $"({seed}, {increment})"));
        }
    }

    protected override void IndexTraits(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (operation[SqlServerAnnotationNames.Clustered] is bool clustered)
        {
            builder.Append(clustered ? "CLUSTERED " : "NONCLUSTERED ");
        }
    }

    // INCLUDE (...), then the filter, then WITH (...), as T-SQL orders them. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Migrations/SqlServerMigrationsSqlGenerator.cs
    protected override void IndexOptions(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if ((operation[SqlServerAnnotationNames.Include] is IReadOnlyList<string> includes) && (includes.Count > 0))
        {
            builder
                .Append(" INCLUDE (")
                .Append(string.Join(", ", includes.Select(c => Dependencies.SqlGenerationHelper.DelimitIdentifier(c))))
                .Append(")");
        }

        base.IndexOptions(operation, model, builder);

        var options = new List<string>();
        if (operation[SqlServerAnnotationNames.FillFactor] is int fillFactor)
        {
            options.Add("FILLFACTOR = " + fillFactor.ToString(CultureInfo.InvariantCulture));
        }

        if (operation[SqlServerAnnotationNames.CreatedOnline] is true)
        {
            options.Add("ONLINE = ON");
        }

        if (operation[SqlServerAnnotationNames.SortInTempDb] is true)
        {
            options.Add("SORT_IN_TEMPDB = ON");
        }

        if (operation[SqlServerAnnotationNames.DataCompression] is DataCompressionType compression)
        {
            options.Add("DATA_COMPRESSION = " + compression.ToString().ToUpperInvariant());
        }

        if (options.Count > 0)
        {
            builder.Append(" WITH (").Append(string.Join(", ", options)).Append(")");
        }
    }

    // A memory-optimized table is created WITH (MEMORY_OPTIMIZED = ON), outside a transaction, which such DDL refuses. A
    // temporal table is created WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = schema.name)); HISTORY_TABLE needs a schema, so
    // without one the statement runs through EXEC with the session's SCHEMA_NAME(). Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Migrations/SqlServerMigrationsSqlGenerator.cs
    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!terminate && (operation.Comment is not null || operation.Columns.Any(column => column.Comment is not null)))
        {
            throw new ArgumentException("SQL Server cannot generate an unterminated CREATE TABLE statement with comments.", nameof(terminate));
        }

        var memoryOptimized = IsMemoryOptimized(operation, model, operation.Schema, operation.Name);
        var temporal = operation[SqlServerAnnotationNames.IsTemporal] is true;
        if (!memoryOptimized && !temporal)
        {
            base.Generate(operation, model, builder, terminate);
            CreateTableComments(operation, builder);
            return;
        }

        var options = new List<string>();
        var historySchema = operation[SqlServerAnnotationNames.TemporalHistoryTableSchema] as string;
        var historyName = operation[SqlServerAnnotationNames.TemporalHistoryTableName] as string ?? operation.Name + "History";
        if (temporal && (historySchema != null) && (historySchema != operation.Schema))
        {
            Generate(new EnsureSchemaOperation { Name = historySchema }, model, builder);
        }

        if (temporal && (historySchema != null))
        {
            options.Add("SYSTEM_VERSIONING = ON (HISTORY_TABLE = " + Dependencies.SqlGenerationHelper.DelimitIdentifier(historyName, historySchema) + ")");
        }

        if (memoryOptimized)
        {
            options.Add("MEMORY_OPTIMIZED = ON");
        }

        if (temporal && (historySchema == null))
        {
            var table = new MigrationCommandListBuilder(Dependencies);
            base.Generate(operation, model, table, terminate: false);
            table.EndCommand();
            options.Insert(
                0,
                "SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + QUOTENAME(@historyTableSchema) + N'."
                + Dependencies.SqlGenerationHelper.DelimitIdentifier(historyName).Replace("'", "''", StringComparison.Ordinal) + ")");
            builder
                .AppendLine("DECLARE @historyTableSchema sysname = SCHEMA_NAME();")
                .Append("EXEC(N'")
                .Append(table.GetCommandList().Single().CommandText.Replace("'", "''", StringComparison.Ordinal))
                .Append(" WITH (").Append(string.Join(", ", options)).Append(")')");
        }
        else
        {
            base.Generate(operation, model, builder, terminate: false);
            builder.Append(" WITH (").Append(string.Join(", ", options)).Append(")");
        }

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder, suppressTransaction: memoryOptimized);
        }

        CreateTableComments(operation, builder);
    }

    private void CreateTableComments(CreateTableOperation operation, MigrationCommandListBuilder builder)
    {
        if (operation.Comment is { Length: > 0 } tableComment)
        {
            AddDescription(builder, tableComment, operation.Schema, operation.Name);
        }

        foreach (var column in operation.Columns.Where(column => column.Comment is { Length: > 0 }))
        {
            AddDescription(builder, column.Comment!, operation.Schema, operation.Name, column.Name);
        }
    }

    // A temporal table names its period after its columns: PERIOD FOR SYSTEM_TIME (start, end).
    protected override void CreateTableConstraints(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        base.CreateTableConstraints(operation, model, builder);
        if ((operation[SqlServerAnnotationNames.IsTemporal] is not true)
            || (operation[SqlServerAnnotationNames.TemporalPeriodStartColumnName] is not string start)
            || (operation[SqlServerAnnotationNames.TemporalPeriodEndColumnName] is not string end))
        {
            return;
        }

        builder
            .AppendLine(",")
            .Append("PERIOD FOR SYSTEM_TIME(")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(start))
            .Append(", ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(end))
            .Append(")");
    }

    // A temporal table stops versioning before it is dropped, and its history table is dropped after it.
    protected override void Generate(DropTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (operation[SqlServerAnnotationNames.IsTemporal] is not true)
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema))
            .Append(" SET (SYSTEM_VERSIONING = OFF)")
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
        base.Generate(operation, model, builder, terminate: true);
        builder
            .Append("DROP TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(
                operation[SqlServerAnnotationNames.TemporalHistoryTableName] as string ?? operation.Name + "History",
                operation[SqlServerAnnotationNames.TemporalHistoryTableSchema] as string ?? operation.Schema))
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    // Turning versioning on or off, or moving the history table, of an existing table isn't generated: refused, not skipped.
    protected override void Generate(AlterTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        string[] temporalAnnotations =
        [
            SqlServerAnnotationNames.IsTemporal,
            SqlServerAnnotationNames.TemporalHistoryTableName,
            SqlServerAnnotationNames.TemporalHistoryTableSchema
        ];
        if (temporalAnnotations.Any(a => !Equals(operation[a], operation.OldTable[a])))
        {
            throw new NotSupportedException(
                $"'{operation.Name}' changes whether it is a temporal table, or its history table; write that change in the migration with "
                + "migrationBuilder.Sql (ALTER TABLE ... SET (SYSTEM_VERSIONING = ...)).");
        }

        base.Generate(operation, model, builder);
        if (operation.OldTable.Comment == operation.Comment)
        {
            return;
        }

        if (operation.OldTable.Comment is { Length: > 0 })
        {
            DropDescription(builder, operation.Schema, operation.Name);
        }

        if (operation.Comment is { Length: > 0 } comment)
        {
            AddDescription(builder, comment, operation.Schema, operation.Name);
        }

        EndStatement(builder);
    }

    // A memory-optimized table's indexes are part of the table: ALTER TABLE ... ADD INDEX, outside a transaction.
    protected override void Generate(CreateIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!IsMemoryOptimized(operation, model, operation.Schema, operation.Table))
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" ADD INDEX ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
            .Append(" ");
        if (operation.IsUnique)
        {
            builder.Append("UNIQUE ");
        }

        IndexTraits(operation, model, builder);
        builder
            .Append("(")
            .Append(string.Join(", ", operation.Columns.Select(c => Dependencies.SqlGenerationHelper.DelimitIdentifier(c))))
            .Append(")");
        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder, suppressTransaction: true);
        }
    }

    private static bool IsMemoryOptimized(MigrationOperation operation, IModel? model, string? schema, string table)
        => operation[SqlServerAnnotationNames.MemoryOptimized] as bool?
            ?? ((model?.GetRelationalModel().FindTable(table, schema) is { } modelTable) && SqlServerAnnotationProvider.IsMemoryOptimized(modelTable));

    protected override void Generate(RenameColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
        => Rename(builder, ObjectName(operation.Schema, operation.Table, operation.Name), operation.NewName, "COLUMN");

    // Another schema is ALTER SCHEMA ... TRANSFER (a null schema is the default, dbo); a new name, sp_rename after it.
    protected override void Generate(RenameTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        var schema = operation.Schema;
        if (operation.NewSchema != operation.Schema)
        {
            schema = operation.NewSchema ?? "dbo";
            builder
                .Append("ALTER SCHEMA ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(schema))
                .Append(" TRANSFER ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema))
                .AppendLine(";");
            EndStatement(builder);
        }

        if ((operation.NewName != null) && (operation.NewName != operation.Name))
        {
            Rename(builder, ObjectName(schema, operation.Name), operation.NewName, null);
        }
    }

    protected override void Generate(RenameIndexOperation operation, IModel? model, MigrationCommandListBuilder builder)
        => Rename(builder, ObjectName(operation.Schema, operation.Table!, operation.Name), operation.NewName, "INDEX");

    protected override void Generate(DropIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        builder
            .Append("DROP INDEX ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
            .Append(" ON ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table!, operation.Schema));

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
    }

    /// <summary>Type and nullability in one ALTER COLUMN; a default is a separate constraint, dropped and re-added.</summary>
    protected override void Generate(AlterColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        DropDefaultConstraint(operation.Schema, operation.Table, operation.Name, builder);

        var type = operation.ColumnType ?? GetColumnType(operation.Schema, operation.Table, operation.Name, operation, model)!;
        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" ALTER COLUMN ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
            .Append(" ")
            .Append(type)
            .Append(operation.IsNullable ? " NULL" : " NOT NULL")
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);

        if (operation.DefaultValue != null || operation.DefaultValueSql != null)
        {
            builder.Append("ALTER TABLE ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema)).Append(" ADD");
            DefaultValue(operation.DefaultValue, operation.DefaultValueSql, type, builder);
            builder
                .Append(" FOR ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
                .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }

        if (operation.OldColumn.Comment != operation.Comment)
        {
            if (operation.OldColumn.Comment is { Length: > 0 })
            {
                DropDescription(builder, operation.Schema, operation.Table, operation.Name);
            }

            if (operation.Comment is { Length: > 0 } comment)
            {
                AddDescription(builder, comment, operation.Schema, operation.Table, operation.Name);
            }
        }
    }

    protected override void Generate(DropColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        DropDefaultConstraint(operation.Schema, operation.Table, operation.Name, builder);
        base.Generate(operation, model, builder, terminate);
    }

    protected override void Generate(EnsureSchemaOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        builder
            .Append("IF SCHEMA_ID(").Append(Literal(operation.Name)).Append(") IS NULL EXEC(")
            .Append(Literal("CREATE SCHEMA " + Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name)))
            .AppendLine(");");
        EndStatement(builder);
    }

    /// <summary>A default added with a column gets a generated name, which has to be looked up to drop it.</summary>
    private void DropDefaultConstraint(string? schema, string table, string column, MigrationCommandListBuilder builder)
    {
        builder
            .AppendLine("DECLARE @default sysname;")
            .Append("SELECT @default = d.name FROM sys.default_constraints d ")
            .Append("JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id ")
            .Append("WHERE d.parent_object_id = OBJECT_ID(").Append(Literal(ObjectName(schema, table)))
            .Append(") AND c.name = ").Append(Literal(column)).AppendLine(";")
            .Append("IF @default IS NOT NULL EXEC(N'ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table, schema).Replace("'", "''", StringComparison.Ordinal))
            .AppendLine(" DROP CONSTRAINT [' + @default + ']');");
        EndStatement(builder);
    }

    private void Rename(MigrationCommandListBuilder builder, string objectName, string newName, string? type)
    {
        builder.Append("EXEC sp_rename ").Append(Literal(objectName)).Append(", ").Append(Literal(newName));
        if (type != null)
        {
            builder.Append(", ").Append(Literal(type));
        }

        builder.AppendLine(";");
        EndStatement(builder);
    }

    private string ObjectName(string? schema, params string[] parts)
        => string.Join(".", (schema == null ? parts : [schema, .. parts]).Select(Dependencies.SqlGenerationHelper.DelimitIdentifier));

    private void AddDescription(
        MigrationCommandListBuilder builder,
        string description,
        string? schema,
        string table,
        string? column = null)
    {
        var schemaName = schema is null ? "@commentSchema" : Literal(schema);
        if (schema is null)
        {
            builder.AppendLine("DECLARE @commentSchema sysname = SCHEMA_NAME();");
        }

        builder.Append("EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = ")
            .Append(Literal(description))
            .Append(", @level0type = N'SCHEMA', @level0name = ")
            .Append(schemaName)
            .Append(", @level1type = N'TABLE', @level1name = ")
            .Append(Literal(table));
        if (column is not null)
        {
            builder.Append(", @level2type = N'COLUMN', @level2name = ").Append(Literal(column));
        }

        builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    private void DropDescription(MigrationCommandListBuilder builder, string? schema, string table, string? column = null)
    {
        var schemaName = schema is null ? "@commentSchema" : Literal(schema);
        if (schema is null)
        {
            builder.AppendLine("DECLARE @commentSchema sysname = SCHEMA_NAME();");
        }

        builder.Append("EXEC sys.sp_dropextendedproperty @name = N'MS_Description', @level0type = N'SCHEMA', @level0name = ")
            .Append(schemaName)
            .Append(", @level1type = N'TABLE', @level1name = ")
            .Append(Literal(table));
        if (column is not null)
        {
            builder.Append(", @level2type = N'COLUMN', @level2name = ").Append(Literal(column));
        }

        builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    private static string Literal(string value)
        => "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}

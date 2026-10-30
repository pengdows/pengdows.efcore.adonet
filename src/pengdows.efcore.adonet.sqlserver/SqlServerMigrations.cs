using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>T-SQL where it differs from ISO: IDENTITY, sp_rename, ALTER COLUMN, and named default constraints.</summary>
internal class SqlServerMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!terminate && (operation.Comment is not null || operation.Columns.Any(column => column.Comment is not null)))
        {
            throw new ArgumentException("SQL Server cannot generate an unterminated CREATE TABLE statement with comments.", nameof(terminate));
        }

        base.Generate(operation, model, builder, terminate);

        // EF's SQL Server annotation is carried onto the create-table operation. Keep the temporal clause in a
        // separate statement so the provider can still use the shared CREATE TABLE column/constraint generator.
        if (operation["SqlServer:IsTemporal"] is true
            || model?.GetEntityTypes().Any(entity =>
                string.Equals(entity.GetTableName(), operation.Name, StringComparison.Ordinal)
                && string.Equals(entity.GetSchema(), operation.Schema, StringComparison.Ordinal)
                && entity.FindAnnotation("SqlServer:IsTemporal")?.Value is true) == true)
        {
            builder
                .Append("ALTER TABLE ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema))
                .AppendLine(" SET (SYSTEM_VERSIONING = ON);");
            EndStatement(builder);
        }

        if (operation.Comment is { Length: > 0 } tableComment)
        {
            AddDescription(builder, tableComment, operation.Schema, operation.Name);
        }

        foreach (var column in operation.Columns.Where(column => column.Comment is { Length: > 0 }))
        {
            AddDescription(builder, column.Comment!, operation.Schema, operation.Name, column.Name);
        }
    }

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

    protected override void IdentityDefinition(string? schema, string table, string name, ColumnOperation operation, MigrationCommandListBuilder builder)
        => builder.Append(" IDENTITY");

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

    protected override void Generate(AlterTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
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

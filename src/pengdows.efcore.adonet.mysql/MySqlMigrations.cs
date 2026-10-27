using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL where it differs from ISO: AUTO_INCREMENT, MODIFY COLUMN, indexes named within their table, and comments (a
///     column's <c>COMMENT</c>, a table's <c>ALTER TABLE ... COMMENT =</c>).
/// </summary>
internal partial class MySqlMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    // Two orders MySQL needs that EF's differ doesn't give, for a key column that is AUTO_INCREMENT (which MySQL requires to
    // lead a key): a table rename drops its primary key and adds it again only to rename it, and MySQL names every one
    // PRIMARY, so neither runs; and an AUTO_INCREMENT the migration removes is removed before its key is dropped, one it adds
    // after its key is added.
    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
        => base.Generate(Reorder(operations), model, options);

    private static List<MigrationOperation> Reorder(IReadOnlyList<MigrationOperation> operations)
    {
        var ordered = operations.ToList();
        foreach (var drop in operations.OfType<DropPrimaryKeyOperation>())
        {
            var table = (drop.Schema, drop.Table);
            var renamedTable = operations.OfType<RenameTableOperation>().FirstOrDefault(r => (r.Schema, r.Name) == table) is { } rename
                ? (rename.NewSchema ?? rename.Schema, rename.NewName ?? rename.Name)
                : table;
            var readd = renamedTable == table
                ? null
                : operations.OfType<AddPrimaryKeyOperation>().FirstOrDefault(a => (a.Schema, a.Table) == renamedTable);
            if (readd != null)
            {
                ordered.Remove(drop);
                ordered.Remove(readd);
                continue;
            }

            var index = ordered.IndexOf(drop);
            foreach (var alter in operations.OfType<AlterColumnOperation>()
                         .Where(a => ((a.Schema, a.Table) == table) && IsIdentity(a.OldColumn) && !IsIdentity(a)))
            {
                ordered.Remove(alter);
                ordered.Insert(index++, alter);
            }
        }

        // An AUTO_INCREMENT the migration adds to a new key's column waits for the key.
        foreach (var add in operations.OfType<AddPrimaryKeyOperation>())
        {
            foreach (var alter in operations.OfType<AlterColumnOperation>()
                         .Where(a => (a.Schema, a.Table) == (add.Schema, add.Table) && IsIdentity(a) && !IsIdentity(a.OldColumn))
                         .ToList())
            {
                ordered.Remove(alter);
                ordered.Insert(ordered.IndexOf(add) + 1, alter);
            }
        }

        return ordered;
    }

    protected override void IdentityDefinition(string? schema, string table, string name, ColumnOperation operation, MigrationCommandListBuilder builder)
        => builder.Append(" AUTO_INCREMENT");

    // InnoDB wants an AUTO_INCREMENT column to lead an index. In a composite key led by another column (an owned
    // collection's owner key, then its own number) it doesn't, so it gets a unique constraint of its own. Behaviour reference
    // (not copied): Pomelo's MySqlMigrationsSqlGenerator (MIT, Pomelo Foundation):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if ((operation.PrimaryKey is { Columns.Length: > 1 } primaryKey)
            && (operation.Columns.FirstOrDefault(c => c[AdoNetAnnotationNames.Identity] is true) is { } autoIncrement)
            && (primaryKey.Columns[0] != autoIncrement.Name)
            && !operation.UniqueConstraints.Any(u => u.Columns.Length > 0 && u.Columns[0] == autoIncrement.Name))
        {
            operation.UniqueConstraints.Add(new AddUniqueConstraintOperation
            {
                Schema = operation.Schema,
                Table = operation.Name,
                Name = $"AK_{operation.Name}_{autoIncrement.Name}",
                Columns = [autoIncrement.Name]
            });
        }

        base.Generate(operation, model, builder, terminate);
        if (operation.Comment != null)
        {
            TableComment(operation.Name, operation.Schema, operation.Comment, builder);
        }
    }

    protected override void Generate(AlterTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (operation.Comment != operation.OldTable.Comment)
        {
            TableComment(operation.Name, operation.Schema, operation.Comment ?? "", builder);
        }
    }

    protected override void ColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        base.ColumnDefinition(schema, table, name, operation, model, builder);
        if (operation.Comment != null)
        {
            builder.Append(" COMMENT ").Append(Dialect.Sql.StringLiteral(operation.Comment));
        }
    }

    private void TableComment(string table, string? schema, string comment, MigrationCommandListBuilder builder)
    {
        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table, schema))
            .Append(" COMMENT = ")
            .Append(Dialect.Sql.StringLiteral(comment))
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    // TEXT, BLOB, JSON and GEOMETRY columns take no literal default, but (MySQL 8.0.13+) take an expression default.
    // Behaviour reference (not copied): Pomelo's MySqlMigrationsSqlGenerator.DefaultValue (MIT, Pomelo Foundation):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    private static readonly string[] ExpressionDefaultTypes =
        ["TINYTEXT", "TEXT", "MEDIUMTEXT", "LONGTEXT", "TINYBLOB", "BLOB", "MEDIUMBLOB", "LONGBLOB", "JSON", "GEOMETRY", "POINT", "LINESTRING", "POLYGON"];

    protected override void DefaultValue(object? defaultValue, string? defaultValueSql, string? columnType, MigrationCommandListBuilder builder)
    {
        var baseType = (columnType ?? "").Split('(')[0].Trim();
        if (!ExpressionDefaultTypes.Contains(baseType, StringComparer.OrdinalIgnoreCase))
        {
            // A typed literal (TIMESTAMP '...') is an expression, which a default takes only in parentheses; the bare string is
            // the literal default.
            if ((defaultValueSql == null) && (defaultValue != null)
                && TypedLiteral().Match(Dependencies.TypeMappingSource.GetMappingForValue(defaultValue).GenerateSqlLiteral(defaultValue)) is { Success: true } typed)
            {
                builder.Append(" DEFAULT ").Append(typed.Groups[1].Value);
                return;
            }

            base.DefaultValue(defaultValue, defaultValueSql, columnType, builder);
            return;
        }

        if (defaultValueSql != null)
        {
            var sql = defaultValueSql.Trim();
            builder.Append(" DEFAULT ").Append(sql.StartsWith('(') && sql.EndsWith(')') ? sql : "(" + sql + ")");
        }
        else if (defaultValue != null)
        {
            var literal = Dependencies.TypeMappingSource.GetMappingForValue(defaultValue).GenerateSqlLiteral(defaultValue);
            builder.Append(" DEFAULT (").Append(literal).Append(")");
        }
    }

    /// <summary>
    ///     MODIFY COLUMN restates the whole column: type, default and nullability together. A generated column can't become
    ///     stored, virtual or plain in place, so it is dropped and added again. Behaviour reference (not copied):
    ///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    /// </summary>
    protected override void Generate(AlterColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        var old = operation.OldColumn;
        if (((operation.ComputedColumnSql != null) || (old.ComputedColumnSql != null))
            && ((operation.ComputedColumnSql == null) != (old.ComputedColumnSql == null) || (operation.IsStored != old.IsStored)))
        {
            Generate(new DropColumnOperation { Schema = operation.Schema, Table = operation.Table, Name = operation.Name }, model, builder);
            var add = new AddColumnOperation
            {
                Schema = operation.Schema,
                Table = operation.Table,
                Name = operation.Name,
                ClrType = operation.ClrType,
                ColumnType = operation.ColumnType,
                IsUnicode = operation.IsUnicode,
                IsFixedLength = operation.IsFixedLength,
                MaxLength = operation.MaxLength,
                Precision = operation.Precision,
                Scale = operation.Scale,
                IsRowVersion = operation.IsRowVersion,
                IsNullable = operation.IsNullable,
                DefaultValue = operation.DefaultValue,
                DefaultValueSql = operation.DefaultValueSql,
                ComputedColumnSql = operation.ComputedColumnSql,
                IsStored = operation.IsStored,
                Comment = operation.Comment,
                Collation = operation.Collation
            };
            add.AddAnnotations(operation.GetAnnotations());
            Generate(add, model, builder);
            return;
        }

        FillNullsWithDefault(operation, builder);
        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" MODIFY COLUMN ");
        ColumnDefinition(operation.Schema, operation.Table, operation.Name, operation, model, builder);
        builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    // A primary key is dropped as such: MySQL names every one PRIMARY.
    protected override void Generate(DropPrimaryKeyOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" DROP PRIMARY KEY");

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex("^(?:TIMESTAMP|DATE|TIME) ('.*')$", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex TypedLiteral();

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

    protected override void Generate(RenameIndexOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table!, operation.Schema))
            .Append(" RENAME INDEX ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
            .Append(" TO ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName))
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }
}

/// <summary>INFORMATION_SCHEMA spans every database on the server; look only in the current one.</summary>
internal class MySqlHistoryRepository(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetHistoryRepository(dependencies, dialect)
{
    protected override string TableExistsSql(string table, string? schema)
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '"
            + table.Replace("'", "''") + "'";
}

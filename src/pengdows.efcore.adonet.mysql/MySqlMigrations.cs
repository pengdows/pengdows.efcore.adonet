using Microsoft.EntityFrameworkCore;
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
    {
        // Migrations written with Pomelo or Oracle's provider carry their annotations: read as this provider's.
        MySqlForeignAnnotations.Normalize(operations);
        return base.Generate(Reorder(operations), model, options);
    }

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

    // What the dialect's server has (all of it for a current server), and how to name it in an error.
    private MySqlServerFeatures Features
        => Dialect.Sql is MySqlSql sql ? sql.Features : MySqlServerFeatures.For(mariaDb: false, version: null);

    private string Server
        => Dialect is MySqlDialect { ServerVersion: { } version } ? Dialect.Name + " " + version : Dialect.Name;

    // Before RENAME COLUMN (MySQL 8.0, MariaDB 10.5.2), CHANGE COLUMN restates the column under its new name, from the
    // migration's target model. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    protected override void Generate(RenameColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (Features.RenameColumn)
        {
            base.Generate(operation, model, builder);
            return;
        }

        var column = model?.GetRelationalModel().FindTable(operation.Table, operation.Schema)?.FindColumn(operation.NewName)
            ?? throw new InvalidOperationException(
                $"Renaming '{operation.Table}.{operation.Name}' on {Server}, which has no RENAME COLUMN, restates the column, which needs the migration's target model.");
        var definition = new AddColumnOperation
        {
            Schema = operation.Schema,
            Table = operation.Table,
            Name = operation.NewName,
            ClrType = column.ProviderClrType,
            ColumnType = column.StoreType,
            IsNullable = column.IsNullable,
            DefaultValue = column.DefaultValue,
            DefaultValueSql = column.DefaultValueSql,
            ComputedColumnSql = column.ComputedColumnSql,
            IsStored = column.IsStored,
            Comment = column.Comment,
            Collation = column.Collation
        };
        definition.AddAnnotations(column.GetAnnotations());
        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" CHANGE COLUMN ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
            .Append(" ");
        ColumnDefinition(operation.Schema, operation.Table, operation.NewName, definition, model, builder);
        builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

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

        // The table's DEFAULT CHARACTER SET, COLLATE and other options go after its definition, before the terminator.
        base.Generate(operation, model, builder, terminate: false);
        if (operation[MySqlModelOptionsExtensions.CharSetAnnotation] is string charSet)
        {
            builder.Append(" DEFAULT CHARACTER SET ").Append(charSet);
        }

        if (operation[MySqlModelOptionsExtensions.CollationAnnotation] is string collation)
        {
            builder.Append(" COLLATE ").Append(collation);
        }

        TableOptions(MySqlTableOptions.Deserialize(operation[MySqlModelOptionsExtensions.StoreOptionsAnnotation] as string), builder);

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }

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

        // A changed default character set or collation, which the columns already there keep.
        var charSet = operation[MySqlModelOptionsExtensions.CharSetAnnotation] as string;
        var collation = operation[MySqlModelOptionsExtensions.CollationAnnotation] as string;
        if (((charSet != null) && (charSet != (operation.OldTable[MySqlModelOptionsExtensions.CharSetAnnotation] as string)))
            || ((collation != null) && (collation != (operation.OldTable[MySqlModelOptionsExtensions.CollationAnnotation] as string))))
        {
            builder.Append("ALTER TABLE ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema));
            if (charSet != null)
            {
                builder.Append(" CHARACTER SET ").Append(charSet);
            }

            if (collation != null)
            {
                builder.Append(" COLLATE ").Append(collation);
            }

            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }

        // The table options added or changed; one removed keeps its value, as Pomelo's do, since MySQL has no way to unset one.
        var oldOptions = MySqlTableOptions.Deserialize(operation.OldTable[MySqlModelOptionsExtensions.StoreOptionsAnnotation] as string);
        var changed = MySqlTableOptions.Deserialize(operation[MySqlModelOptionsExtensions.StoreOptionsAnnotation] as string)
            .Where(o => !(oldOptions.TryGetValue(o.Key, out var old) && (old == o.Value)))
            .ToDictionary(o => o.Key, o => o.Value);
        if (changed.Count > 0)
        {
            builder.Append("ALTER TABLE ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema));
            TableOptions(changed, builder);
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
    }

    // Each " name=value", as the model gave them (validated by HasTableOption).
    private static void TableOptions(Dictionary<string, string> options, MigrationCommandListBuilder builder)
    {
        foreach (var (name, value) in options)
        {
            builder.Append(" ").Append(name).Append("=").Append(value);
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
        // A column's CHARACTER SET follows its type.
        if ((operation[MySqlModelOptionsExtensions.CharSetAnnotation] is string charSet) && (operation.ComputedColumnSql == null))
        {
            operation.ColumnType = (operation.ColumnType ?? GetColumnType(schema, table, name, operation, model)) + " CHARACTER SET " + charSet;
        }

        // A GUID column without a collation of its own takes the model's GUID collation.
        if ((operation.Collation == null)
            && (operation.ComputedColumnSql == null)
            && (operation[MySqlModelOptionsExtensions.GuidCollationAnnotation] is string guidCollation))
        {
            operation.Collation = MySqlModelOptionsExtensions.Name(guidCollation, nameof(operation));
        }

        // A text column without a collation of its own takes the one its entity type's or the model's delegation gives it.
        if ((operation.Collation == null)
            && (operation.ComputedColumnSql == null)
            && (operation[MySqlAnnotationProvider.InheritedCollationAnnotation] is string inheritedCollation))
        {
            operation.Collation = MySqlModelOptionsExtensions.Name(inheritedCollation, nameof(operation));
        }

        base.ColumnDefinition(schema, table, name, operation, model, builder);

        // A row version is a TIMESTAMP(6) the server sets on every insert and update; a computed column (UseMySqlComputedColumn)
        // is a DATETIME or TIMESTAMP it sets so, at the column's precision. Behaviour reference (not copied):
        // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
        var columnType = operation.ColumnType ?? GetColumnType(schema, table, name, operation, model) ?? "";
        var strategy = operation[MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation] as string;
        var computed = strategy is MySqlModelOptionsExtensions.ComputedColumnStrategy;
        if ((operation.DefaultValueSql == null)
            && (operation.DefaultValue == null)
            && (operation.ComputedColumnSql == null)
            && (DateTimeType().Match(columnType) is { Success: true } dateTime))
        {
            // An identity date and time (UseMySqlIdentityColumn) is set on insert only.
            var now = "CURRENT_TIMESTAMP" + dateTime.Groups["precision"].Value;
            if ((operation.IsRowVersion && dateTime.Groups["type"].Value.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) || computed)
            {
                builder.Append(" DEFAULT ").Append(now).Append(" ON UPDATE ").Append(now);
            }
            else if (strategy is MySqlModelOptionsExtensions.IdentityColumnStrategy)
            {
                builder.Append(" DEFAULT ").Append(now);
            }
        }

        if (operation.Comment != null)
        {
            builder.Append(" COMMENT ").Append(Dialect.Sql.StringLiteral(operation.Comment));
        }

        // A geometry column's spatial reference system: MySQL's SRID attribute (8.0.3), MariaDB's REF_SYSTEM_ID, which its
        // grammar takes after COMMENT (https://mariadb.com/kb/en/create-table/#column-definitions). Behaviour reference (not
        // copied):
        // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
        if (operation[MySqlModelOptionsExtensions.SpatialReferenceSystemAnnotation] is int srid)
        {
            // MySQL before 8.0.3 has no SRID column attribute: refused rather than leaving the column unrestricted.
            if (!Features.ColumnSrid)
            {
                throw new NotSupportedException($"Column '{table}.{name}' has a spatial reference system, which {Server} can't restrict a column to (MySQL 8.0.3).");
            }

            builder
                .Append(Dialect.Sql is MySqlSql { IsMariaDb: true } ? " REF_SYSTEM_ID=" : " SRID ")
                .Append(srid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    // A prefix length indexes a column's first characters: col(n).
    protected override void GenerateIndexColumnList(CreateIndexOperation operation, IModel? model, MigrationCommandListBuilder builder)
        => PrefixedColumns(operation, operation.Columns, operation.IsDescending, builder);

    // A key's prefix lengths, as an index's. Otherwise as EF's own constraint.
    protected override void PrimaryKeyConstraint(AddPrimaryKeyOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        ConstraintName(operation.Name, builder);
        builder.Append("PRIMARY KEY ");
        IndexTraits(operation, model, builder);
        builder.Append("(");
        PrefixedColumns(operation, operation.Columns, null, builder);
        builder.Append(")");
    }

    protected override void UniqueConstraint(AddUniqueConstraintOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        ConstraintName(operation.Name, builder);
        builder.Append("UNIQUE ");
        IndexTraits(operation, model, builder);
        builder.Append("(");
        PrefixedColumns(operation, operation.Columns, null, builder);
        builder.Append(")");
    }

    private void ConstraintName(string? name, MigrationCommandListBuilder builder)
    {
        if (name != null)
        {
            builder.Append("CONSTRAINT ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name)).Append(" ");
        }
    }

    private void PrefixedColumns(MigrationOperation operation, string[] columns, bool[]? isDescending, MigrationCommandListBuilder builder)
    {
        var prefixLengths = operation[MySqlModelOptionsExtensions.PrefixLengthAnnotation] as int[] ?? [];
        for (var i = 0; i < columns.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(columns[i]));
            if ((i < prefixLengths.Length) && (prefixLengths[i] > 0))
            {
                builder.Append("(").Append(prefixLengths[i].ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(")");
            }

            // MySQL before 8.0.1 parses DESC and ignores it: written only where it orders the index.
            if (Features.DescendingIndexes && (isDescending is not null) && ((isDescending.Length == 0) || isDescending[i]))
            {
                builder.Append(" DESC");
            }
        }
    }

    // A FULLTEXT index's parser follows its columns. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    protected override void IndexOptions(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if ((operation[MySqlFullTextExtensions.FullTextAnnotation] is true)
            && (operation[MySqlFullTextExtensions.FullTextParserAnnotation] is string parser))
        {
            builder.Append(" WITH PARSER ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(parser));
        }

        base.IndexOptions(operation, model, builder);
    }

    // CREATE FULLTEXT INDEX for an index marked IsFullText.
    protected override void IndexTraits(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (operation[MySqlFullTextExtensions.FullTextAnnotation] is true)
        {
            builder.Append("FULLTEXT ");
        }

        if (operation[MySqlModelOptionsExtensions.SpatialAnnotation] is true)
        {
            builder.Append("SPATIAL ");
        }

        base.IndexTraits(operation, model, builder);
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

            // Before expression defaults (MySQL 8.0.13, MariaDB 10.2.7) a default is a literal or CURRENT_TIMESTAMP, written bare.
            if ((defaultValueSql != null) && !Features.ExpressionDefaults)
            {
                builder.Append(" DEFAULT ").Append(defaultValueSql.Trim());
                return;
            }

            base.DefaultValue(defaultValue, defaultValueSql, columnType, builder);
            return;
        }

        if (((defaultValueSql != null) || (defaultValue != null)) && !Features.ExpressionDefaults)
        {
            throw new NotSupportedException(
                $"A {baseType} column takes a default only as an expression, which {Server} doesn't have (MySQL 8.0.13, MariaDB 10.2.7).");
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

    // EF finds a generator by an operation's exact type: Pomelo's drop-and-recreate operations go to their base types' here.
    protected override void Generate(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        switch (operation)
        {
            case MySqlDropPrimaryKeyAndRecreateForeignKeysOperation dropPrimaryKey:
                Recreating(dropPrimaryKey.RecreateForeignKeys, dropPrimaryKey.Table, dropPrimaryKey.Schema, model, builder,
                    () => Generate(dropPrimaryKey, model, builder));
                return;
            case MySqlDropUniqueConstraintAndRecreateForeignKeysOperation dropUniqueConstraint:
                Recreating(dropUniqueConstraint.RecreateForeignKeys, dropUniqueConstraint.Table, dropUniqueConstraint.Schema, model, builder,
                    () => Generate(dropUniqueConstraint, model, builder));
                return;
            default:
                base.Generate(operation, model, builder);
                return;
        }
    }

    // With recreateForeignKeys, the table's foreign keys in the target model are dropped before and added again after, since
    // MySQL may be using the key's index for one whose columns lead it, and then refuses to drop it. Behaviour reference (not
    // copied): Pomelo's TemporarilyDropForeignKeys:
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    private void Recreating(bool recreate, string table, string? schema, IModel? model, MigrationCommandListBuilder builder, Action drop)
    {
        if (!recreate)
        {
            drop();
            return;
        }

        var foreignKeys = (model ?? throw new InvalidOperationException(
                    $"Recreating the foreign keys of '{table}' needs the migration's target model, to know which they are."))
            .GetRelationalModel().FindTable(table, schema)?.ForeignKeyConstraints.ToArray() ?? [];
        foreach (var foreignKey in foreignKeys)
        {
            Generate(new DropForeignKeyOperation { Schema = foreignKey.Table.Schema, Table = foreignKey.Table.Name, Name = foreignKey.Name }, model, builder);
        }

        drop();
        foreach (var foreignKey in foreignKeys)
        {
            Generate(
                new AddForeignKeyOperation
                {
                    Schema = foreignKey.Table.Schema,
                    Table = foreignKey.Table.Name,
                    Name = foreignKey.Name,
                    Columns = foreignKey.Columns.Select(c => c.Name).ToArray(),
                    PrincipalSchema = foreignKey.PrincipalTable.Schema,
                    PrincipalTable = foreignKey.PrincipalTable.Name,
                    PrincipalColumns = foreignKey.PrincipalColumns.Select(c => c.Name).ToArray(),
                    OnDelete = foreignKey.OnDeleteAction
                },
                model,
                builder);
        }
    }

    // DROP FOREIGN KEY and DROP INDEX, which every MySQL and MariaDB version takes; ISO's DROP CONSTRAINT came to MySQL only
    // in 8.0.19. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    protected override void Generate(DropForeignKeyOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        => DropFromTable(operation.Table, operation.Schema, "FOREIGN KEY", operation.Name, builder, terminate);

    protected override void Generate(DropUniqueConstraintOperation operation, IModel? model, MigrationCommandListBuilder builder)
        => DropFromTable(operation.Table, operation.Schema, "INDEX", operation.Name, builder, terminate: true);

    private void DropFromTable(string table, string? schema, string kind, string name, MigrationCommandListBuilder builder, bool terminate)
    {
        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table, schema))
            .Append(" DROP ")
            .Append(kind)
            .Append(" ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name));

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
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

    // DATETIME or TIMESTAMP, with its fractional precision as written: "(6)".
    [System.Text.RegularExpressions.GeneratedRegex(@"^(?<type>datetime|timestamp)(?<precision>\(\d\))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex DateTimeType();

    // The database's collation, or else its character set: a collation is the more specific. Behaviour reference (not
    // copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    protected override void Generate(AlterDatabaseOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        // A new database's operation (EF's differ from no model) carries its collation only as the annotation.
        var charSet = operation[MySqlModelOptionsExtensions.CharSetAnnotation] as string;
        var collation = operation.Collation ?? operation[MySqlModelOptionsExtensions.CollationAnnotation] as string;
        var oldCollation = operation.OldDatabase.Collation ?? operation.OldDatabase[MySqlModelOptionsExtensions.CollationAnnotation] as string;
        if ((collation != null) && (collation != oldCollation))
        {
            builder.Append("ALTER DATABASE COLLATE ")
                .Append(MySqlModelOptionsExtensions.Name(collation, nameof(operation)))
                .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
            return;
        }

        if ((charSet != null) && (charSet != (operation.OldDatabase[MySqlModelOptionsExtensions.CharSetAnnotation] as string)))
        {
            builder.Append("ALTER DATABASE CHARACTER SET ")
                .Append(MySqlModelOptionsExtensions.Name(charSet, nameof(operation)))
                .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
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

    // GET_LOCK waits for the lock up to the timeout (in whole seconds) and holds it for the connection, so a second migrator
    // on another connection waits rather than fails. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/Internal/MySqlHistoryRepository.cs
    protected override string? SessionLockSql(TimeSpan timeout)
        => "SELECT GET_LOCK('" + LockName + "', " + (long)timeout.TotalSeconds + ")";

    protected override string? SessionUnlockSql
        => "SELECT RELEASE_LOCK('" + LockName + "')";

    private const string LockName = "__EFMigrationsLock";
}

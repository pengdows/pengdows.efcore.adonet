using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Metadata;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>PostgreSQL requires generated columns to use the SQL:2003 STORED form.</summary>
internal sealed class PostgreSqlMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    // Every table has PostgreSQL's system columns (xmin, a row version); a model may map them, but no migration declares,
    // alters or drops one. Behaviour reference (not copied):
    // https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Migrations/NpgsqlMigrationsSqlGenerator.cs
    private static readonly HashSet<string> SystemColumns = new(StringComparer.Ordinal) { "tableoid", "xmin", "cmin", "xmax", "cmax", "ctid" };

    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        operation.Columns.RemoveAll(c => SystemColumns.Contains(c.Name));
        base.Generate(operation, model, builder, terminate);
        if (operation.Comment is not null)
        {
            TableComment(operation.Name, operation.Schema, operation.Comment, builder);
        }

        foreach (var column in operation.Columns.Where(c => c.Comment is not null))
        {
            ColumnComment(operation.Name, operation.Schema, column.Name, column.Comment, builder);
        }
    }

    protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!SystemColumns.Contains(operation.Name))
        {
            base.Generate(operation, model, builder, terminate);
            if (operation.Comment is not null)
            {
                ColumnComment(operation.Table, operation.Schema, operation.Name, operation.Comment, builder);
            }
        }
    }

    protected override void Generate(AlterColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (!SystemColumns.Contains(operation.Name))
        {
            ApplyTsVectorColumnSql(operation);
            ApplyTsVectorColumnSql(operation.OldColumn);
            base.Generate(operation, model, builder);
            if (operation.Comment != operation.OldColumn.Comment)
            {
                ColumnComment(operation.Table, operation.Schema, operation.Name, operation.Comment, builder);
            }
        }
    }

    // A table comment is COMMENT ON TABLE; the core generator has no table comment, so it was dropped. Clearing one is IS NULL.
    protected override void Generate(AlterTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        TableComment(operation.Name, operation.Schema, operation.Comment, builder);
    }

    private void ColumnComment(string table, string? schema, string column, string? comment, MigrationCommandListBuilder builder)
    {
        builder
            .Append("COMMENT ON COLUMN ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table, schema))
            .Append(".")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(column))
            .Append(" IS ")
            .Append(comment is null ? "NULL" : Dialect.Sql.StringLiteral(comment))
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    private void TableComment(string table, string? schema, string? comment, MigrationCommandListBuilder builder)
    {
        builder
            .Append("COMMENT ON TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table, schema))
            .Append(" IS ")
            .Append(comment is null ? "NULL" : Dialect.Sql.StringLiteral(comment))
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    protected override void Generate(DropColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!SystemColumns.Contains(operation.Name))
        {
            base.Generate(operation, model, builder, terminate);
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
        ApplyTsVectorColumnSql(operation);
        base.ColumnDefinition(schema, table, name, operation, model, builder);
        if ((operation.ComputedColumnSql != null) && (operation.IsStored != true))
        {
            builder.Append(" STORED");
        }
    }

    // GENERATED ALWAYS or BY DEFAULT AS IDENTITY, with the sequence's options.
    protected override void IdentityDefinition(string? schema, string table, string name, ColumnOperation operation, MigrationCommandListBuilder builder)
    {
        builder.Append(
            operation[PostgreSqlAnnotations.IdentityStrategy] is PostgreSqlAnnotations.Always
                ? " GENERATED ALWAYS AS IDENTITY"
                : " GENERATED BY DEFAULT AS IDENTITY");
        if ((operation[PostgreSqlAnnotations.IdentityOptions] is string text) && (PostgreSqlIdentityOptions.Parse(text).ToSql() is { Length: > 0 } options))
        {
            builder.Append(" (").Append(options).Append(")");
        }
    }

    // Extensions, enum types and collations the model declares, created before anything that uses them and dropped when the
    // model no longer does. Behaviour reference (not copied): NpgsqlMigrationsSqlGenerator (link above).
    protected override void Generate(AlterDatabaseOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        base.Generate(operation, model, builder);
        var current = operation.GetAnnotations().ToDictionary(a => a.Name, a => a.Value);
        var old = operation.OldDatabase.GetAnnotations().ToDictionary(a => a.Name, a => a.Value);

        foreach (var (annotation, value) in current.Where(a => a.Key.StartsWith(PostgreSqlAnnotations.ExtensionPrefix, StringComparison.Ordinal)))
        {
            if (!old.ContainsKey(annotation))
            {
                CreateExtension(PostgreSqlAnnotations.Parse(PostgreSqlAnnotations.ExtensionPrefix, annotation), value as string, builder);
            }
        }

        foreach (var (annotation, value) in current.Where(a => a.Key.StartsWith(PostgreSqlAnnotations.CollationPrefix, StringComparison.Ordinal)))
        {
            if (!old.ContainsKey(annotation))
            {
                CreateCollation(PostgreSqlAnnotations.Parse(PostgreSqlAnnotations.CollationPrefix, annotation), PostgreSqlCollation.Parse((string)value!), builder);
            }
        }

        foreach (var (annotation, value) in current.Where(a => a.Key.StartsWith(PostgreSqlAnnotations.EnumPrefix, StringComparison.Ordinal)))
        {
            var name = PostgreSqlAnnotations.Parse(PostgreSqlAnnotations.EnumPrefix, annotation);
            var labels = (string[])value!;
            if (old.GetValueOrDefault(annotation) is string[] oldLabels)
            {
                AddEnumLabels(name, oldLabels, labels, builder);
                continue;
            }

            Statement(builder, "CREATE TYPE " + Delimit(name) + " AS ENUM (" + string.Join(", ", labels.Select(Dialect.Sql.StringLiteral)) + ")");
        }

        foreach (var annotation in old.Keys.Where(a => !current.ContainsKey(a)))
        {
            var drop = annotation.StartsWith(PostgreSqlAnnotations.EnumPrefix, StringComparison.Ordinal)
                ? "DROP TYPE " + Delimit(PostgreSqlAnnotations.Parse(PostgreSqlAnnotations.EnumPrefix, annotation))
                : annotation.StartsWith(PostgreSqlAnnotations.CollationPrefix, StringComparison.Ordinal)
                    ? "DROP COLLATION " + Delimit(PostgreSqlAnnotations.Parse(PostgreSqlAnnotations.CollationPrefix, annotation))
                    : annotation.StartsWith(PostgreSqlAnnotations.ExtensionPrefix, StringComparison.Ordinal)
                        ? "DROP EXTENSION " + Delimit((null, PostgreSqlAnnotations.Parse(PostgreSqlAnnotations.ExtensionPrefix, annotation).Name))
                        : null;
            if (drop != null)
            {
                Statement(builder, drop);
            }
        }
    }

    private void CreateExtension((string? Schema, string Name) extension, string? version, MigrationCommandListBuilder builder)
    {
        var sql = "CREATE EXTENSION IF NOT EXISTS " + Delimit((null, extension.Name));
        if (extension.Schema != null)
        {
            sql += " SCHEMA " + Delimit((null, extension.Schema));
        }

        if (!string.IsNullOrEmpty(version))
        {
            sql += " VERSION " + Delimit((null, version));
        }

        Statement(builder, sql);
    }

    private void CreateCollation((string? Schema, string Name) name, PostgreSqlCollation collation, MigrationCommandListBuilder builder)
    {
        var options = new List<string> { "LOCALE = " + Dialect.Sql.StringLiteral(collation.Locale) };
        if (collation.Provider != null)
        {
            options.Add("PROVIDER = " + Delimit((null, collation.Provider)));
        }

        if (collation.Deterministic is { } deterministic)
        {
            options.Add("DETERMINISTIC = " + (deterministic ? "true" : "false"));
        }

        Statement(builder, "CREATE COLLATION " + Delimit(name) + " (" + string.Join(", ", options) + ")");
    }

    // Labels can only be added, each after its predecessor; ALTER TYPE ... ADD VALUE can't run in a transaction block before
    // PostgreSQL 12, so it runs outside one, as Npgsql's does.
    private void AddEnumLabels((string? Schema, string Name) name, string[] oldLabels, string[] labels, MigrationCommandListBuilder builder)
    {
        if (oldLabels.Except(labels).Any())
        {
            throw new NotSupportedException($"PostgreSQL can't remove a label from the enum type '{name.Name}'; drop and create the type in a migration of your own.");
        }

        for (var i = 0; i < labels.Length; i++)
        {
            if (oldLabels.Contains(labels[i]))
            {
                continue;
            }

            var position = i == 0 ? " BEFORE " + Dialect.Sql.StringLiteral(labels[1]) : " AFTER " + Dialect.Sql.StringLiteral(labels[i - 1]);
            builder
                .Append("ALTER TYPE " + Delimit(name) + " ADD VALUE " + Dialect.Sql.StringLiteral(labels[i]) + position)
                .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder, suppressTransaction: true);
        }
    }

    // CREATE [UNIQUE] INDEX [CONCURRENTLY] name ON table [USING method] (column [opclass] [DESC], ...) [INCLUDE (...)]
    // [NULLS NOT DISTINCT] [WHERE filter]. A concurrent index can't be created in a transaction. Behaviour reference (not
    // copied): NpgsqlMigrationsSqlGenerator (link above).
    protected override void Generate(CreateIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        var helper = Dependencies.SqlGenerationHelper;
        var concurrently = operation[PostgreSqlAnnotations.CreatedConcurrently] is true;
        builder.Append("CREATE ");
        if (operation.IsUnique)
        {
            builder.Append("UNIQUE ");
        }

        builder.Append("INDEX ");
        if (concurrently)
        {
            builder.Append("CONCURRENTLY ");
        }

        builder.Append(helper.DelimitIdentifier(operation.Name)).Append(" ON ").Append(helper.DelimitIdentifier(operation.Table, operation.Schema));
        if (operation[PostgreSqlAnnotations.IndexMethod] is string { Length: > 0 } method)
        {
            builder.Append(" USING ").Append(method);
        }

        builder.Append(" (");
        var operators = operation[PostgreSqlAnnotations.IndexOperators] as string[] ?? [];
        for (var i = 0; i < operation.Columns.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(helper.DelimitIdentifier(operation.Columns[i]));
            if ((i < operators.Length) && !string.IsNullOrEmpty(operators[i]))
            {
                builder.Append(" ").Append(operators[i]);
            }

            if ((operation.IsDescending is not null) && ((operation.IsDescending.Length == 0) || operation.IsDescending[i]))
            {
                builder.Append(" DESC");
            }
        }

        builder.Append(")");
        if (operation[PostgreSqlAnnotations.IndexInclude] is string[] { Length: > 0 } include)
        {
            builder.Append(" INCLUDE (").Append(string.Join(", ", include.Select(c => helper.DelimitIdentifier(c)))).Append(")");
        }

        if (operation[PostgreSqlAnnotations.NullsDistinct] is false)
        {
            builder.Append(" NULLS NOT DISTINCT");
        }

        IndexOptions(operation, model, builder);
        if (terminate)
        {
            builder.AppendLine(helper.StatementTerminator);
            EndStatement(builder, suppressTransaction: concurrently);
        }
    }

    private void Statement(MigrationCommandListBuilder builder, string sql)
    {
        builder.Append(sql).AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    private string Delimit((string? Schema, string Name) name)
        => Dependencies.SqlGenerationHelper.DelimitIdentifier(name.Name, name.Schema);

    // A generated tsvector column: to_tsvector over its text columns joined by spaces, a null one taken as empty. Behaviour
    // reference (not copied): NpgsqlMigrationsSqlGenerator.ColumnsToTsVector (link above).
    private void ApplyTsVectorColumnSql(ColumnOperation column)
    {
        if ((column[PostgreSqlFullTextSearchModelExtensions.TsVectorConfigAnnotation] is not string config)
            || (column[PostgreSqlFullTextSearchModelExtensions.TsVectorPropertiesAnnotation] is not string[] columns))
        {
            return;
        }

        var helper = Dependencies.SqlGenerationHelper;
        var text = columns.Select(TsVectorColumn.Parse)
            .Select(c => c.IsNullable ? "coalesce(" + helper.DelimitIdentifier(c.Name) + ", '')" : helper.DelimitIdentifier(c.Name));
        column.ComputedColumnSql = "to_tsvector(" + Dialect.Sql.StringLiteral(config) + ", " + string.Join(" || ' ' || ", text) + ")";
        column.IsStored = true;
    }
}

/// <summary>
///     A column a generated tsvector column reads, as its migration needs it; carried in the operation as text (<c>?name</c> for
///     a nullable one) so the differ compares it as it compares any annotation.
/// </summary>
internal sealed record TsVectorColumn(string Name, bool IsNullable)
{
    public override string ToString()
        => (IsNullable ? "?" : "!") + Name;

    public static TsVectorColumn Parse(string text)
        => new(text[1..], text[0] == '?');
}

/// <summary>Carries PostgreSQL's model options from the model to the operations migrations write.</summary>
internal sealed class PostgreSqlAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : AdoNetAnnotationProvider(dependencies)
{
    private static readonly string[] DatabaseObjectPrefixes =
        [PostgreSqlAnnotations.ExtensionPrefix, PostgreSqlAnnotations.EnumPrefix, PostgreSqlAnnotations.CollationPrefix];

    // The database's extensions, enum types and collations, which the differ compares as the database's annotations.
    public override IEnumerable<IAnnotation> For(IRelationalModel model, bool designTime)
        => base.For(model, designTime)
            .Concat(model.Model.GetAnnotations().Where(a => DatabaseObjectPrefixes.Any(p => a.Name.StartsWith(p, StringComparison.Ordinal))));

    public override IEnumerable<IAnnotation> For(ITableIndex index, bool designTime)
    {
        foreach (var annotation in base.For(index, designTime))
        {
            yield return annotation;
        }

        var mapped = index.MappedIndexes.First();
        foreach (var name in new[]
                 {
                     PostgreSqlAnnotations.IndexMethod, PostgreSqlAnnotations.IndexOperators, PostgreSqlAnnotations.NullsDistinct,
                     PostgreSqlAnnotations.CreatedConcurrently
                 })
        {
            if (mapped.FindAnnotation(name)?.Value is { } value)
            {
                yield return new Annotation(name, value);
            }
        }

        // INCLUDE names columns; the model names properties.
        if (mapped[PostgreSqlAnnotations.IndexInclude] is string[] included)
        {
            var table = StoreObjectIdentifier.Table(index.Table.Name, index.Table.Schema);
            var entityType = mapped.DeclaringEntityType;
            yield return new Annotation(
                PostgreSqlAnnotations.IndexInclude,
                included.Select(name => (entityType.FindProperty(name)
                        ?? throw new InvalidOperationException($"The index '{index.Name}' includes '{name}', which is no property of '{entityType.DisplayName()}'."))
                    .GetColumnName(table) ?? name).ToArray());
        }
    }

    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        // An identity column's kind, its own or the model's, and its sequence's options.
        if (IsIdentity(column))
        {
            var identity = column.PropertyMappings.Select(m => m.Property).First();
            if ((identity.FindAnnotation(PostgreSqlAnnotations.IdentityStrategy)?.Value
                    ?? identity.DeclaringType.Model.FindAnnotation(PostgreSqlAnnotations.IdentityStrategy)?.Value) is string strategy)
            {
                yield return new Annotation(PostgreSqlAnnotations.IdentityStrategy, strategy);
            }

            if (identity.FindAnnotation(PostgreSqlAnnotations.IdentityOptions)?.Value is string options)
            {
                yield return new Annotation(PostgreSqlAnnotations.IdentityOptions, options);
            }
        }

        // A generated tsvector column names its properties; its migration needs their columns in this table.
        var property = column.PropertyMappings.Select(m => m.Property)
            .FirstOrDefault(p => p.FindAnnotation(PostgreSqlFullTextSearchModelExtensions.TsVectorConfigAnnotation) != null);
        if (property != null)
        {
            var table = StoreObjectIdentifier.Table(column.Table.Name, column.Table.Schema);
            var included = ((string[])property[PostgreSqlFullTextSearchModelExtensions.TsVectorPropertiesAnnotation]!)
                .Select(name => property.DeclaringType.FindProperty(name)
                    ?? throw new InvalidOperationException($"The generated tsvector column '{column.Name}' includes '{name}', which is no property of '{property.DeclaringType.DisplayName()}'."))
                .Select(p => new TsVectorColumn(p.GetColumnName(table) ?? p.GetColumnName(), p.IsNullable).ToString())
                .ToArray();
            yield return new Annotation(
                PostgreSqlFullTextSearchModelExtensions.TsVectorConfigAnnotation,
                property[PostgreSqlFullTextSearchModelExtensions.TsVectorConfigAnnotation]);
            yield return new Annotation(PostgreSqlFullTextSearchModelExtensions.TsVectorPropertiesAnnotation, included);
        }
    }
}

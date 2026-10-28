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
    }

    protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!SystemColumns.Contains(operation.Name))
        {
            base.Generate(operation, model, builder, terminate);
        }
    }

    protected override void Generate(AlterColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (!SystemColumns.Contains(operation.Name))
        {
            ApplyTsVectorColumnSql(operation);
            ApplyTsVectorColumnSql(operation.OldColumn);
            base.Generate(operation, model, builder);
        }
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
    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
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

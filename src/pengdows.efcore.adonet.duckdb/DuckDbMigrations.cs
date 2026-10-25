using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.duckdb;

/// <summary>
///     DuckDB has no identity columns: a generated key defaults to <c>nextval</c> of a sequence created with its table.
///     Foreign keys can't cascade, so referential actions are left to EF's client-side cascade.
/// </summary>
internal class DuckDbMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        foreach (var column in operation.Columns.Where(IsIdentity))
        {
            builder
                .Append("CREATE SEQUENCE ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(SequenceName(operation.Name, column.Name), operation.Schema))
                .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }

        base.Generate(operation, model, builder, terminate);
    }

    /// <summary>DuckDB can't add a column with a constraint: add it with its default, then make it required.</summary>
    protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (operation.IsNullable)
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        operation.IsNullable = true;
        try
        {
            base.Generate(operation, model, builder, terminate: true);
        }
        finally
        {
            operation.IsNullable = false;
        }

        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" ALTER COLUMN ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
            .Append(" SET NOT NULL")
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    // nextval takes the sequence's name as a string: delimited and qualified as in DDL, then quoted as a literal.
    protected override void IdentityDefinition(string? schema, string table, string name, ColumnOperation operation, MigrationCommandListBuilder builder)
        => builder
            .Append(" DEFAULT nextval('")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(SequenceName(table, name), schema).Replace("'", "''", StringComparison.Ordinal))
            .Append("')");

    protected override void Generate(DropTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        base.Generate(operation, model, builder, terminate);

        if (operation[AdoNetAnnotationNames.IdentityColumns] is string[] columns)
        {
            foreach (var column in columns)
            {
                builder
                    .Append("DROP SEQUENCE ")
                    .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(SequenceName(operation.Name, column), operation.Schema))
                    .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
                EndStatement(builder);
            }
        }
    }

    protected override void ForeignKeyConstraint(AddForeignKeyOperation operation, IModel? model, MigrationCommandListBuilder builder)
        => base.ForeignKeyConstraint(
            new AddForeignKeyOperation
            {
                Name = operation.Name,
                Schema = operation.Schema,
                Table = operation.Table,
                Columns = operation.Columns,
                PrincipalSchema = operation.PrincipalSchema,
                PrincipalTable = operation.PrincipalTable,
                PrincipalColumns = operation.PrincipalColumns
            },
            model,
            builder);

    private static string SequenceName(string table, string column)
        => $"{table}_{column}_seq";
}

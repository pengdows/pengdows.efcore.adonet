using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.flatfile;

/// <summary>
///     pengdows.flatfile 0.2.1-preview.8 looks a foreign key's table up before CREATE TABLE has added it, so a table can't
///     reference itself there (valid ISO). Such keys are added by ALTER TABLE once the table exists.
/// </summary>
internal class FlatFileMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        var selfReferencing = operation.ForeignKeys
            .Where(fk => (fk.PrincipalTable == operation.Name) && ((fk.PrincipalSchema ?? fk.Schema) == operation.Schema))
            .ToList();
        if (selfReferencing.Count == 0)
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        var table = new CreateTableOperation
        {
            Name = operation.Name,
            Schema = operation.Schema,
            Comment = operation.Comment,
            PrimaryKey = operation.PrimaryKey
        };
        table.AddAnnotations(operation.GetAnnotations());
        table.Columns.AddRange(operation.Columns);
        table.UniqueConstraints.AddRange(operation.UniqueConstraints);
        table.CheckConstraints.AddRange(operation.CheckConstraints);
        table.ForeignKeys.AddRange(operation.ForeignKeys.Except(selfReferencing));

        base.Generate(table, model, builder, terminate: true);
        foreach (var foreignKey in selfReferencing)
        {
            Generate(foreignKey, model, builder, terminate: true);
        }
    }
}

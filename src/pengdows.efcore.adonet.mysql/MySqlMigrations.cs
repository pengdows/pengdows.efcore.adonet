using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.mysql;

/// <summary>MySQL where it differs from ISO: AUTO_INCREMENT, MODIFY COLUMN, and indexes named within their table.</summary>
internal class MySqlMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    protected override void IdentityDefinition(string? schema, string table, string name, ColumnOperation operation, MigrationCommandListBuilder builder)
        => builder.Append(" AUTO_INCREMENT");

    // TEXT, BLOB, JSON and GEOMETRY columns take no literal default, but (MySQL 8.0.13+) take an expression default.
    // Adapted from Pomelo's MySqlMigrationsSqlGenerator.DefaultValue (MIT, Pomelo Foundation):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
    private static readonly string[] ExpressionDefaultTypes =
        ["TINYTEXT", "TEXT", "MEDIUMTEXT", "LONGTEXT", "TINYBLOB", "BLOB", "MEDIUMBLOB", "LONGBLOB", "JSON", "GEOMETRY", "POINT", "LINESTRING", "POLYGON"];

    protected override void DefaultValue(object? defaultValue, string? defaultValueSql, string? columnType, MigrationCommandListBuilder builder)
    {
        var baseType = (columnType ?? "").Split('(')[0].Trim();
        if (!ExpressionDefaultTypes.Contains(baseType, StringComparer.OrdinalIgnoreCase))
        {
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

    /// <summary>MODIFY COLUMN restates the whole column: type, default and nullability together.</summary>
    protected override void Generate(AlterColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" MODIFY COLUMN ");
        ColumnDefinition(operation.Schema, operation.Table, operation.Name, operation, model, builder);
        builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
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

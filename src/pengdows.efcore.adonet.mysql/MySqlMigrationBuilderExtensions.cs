using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     Pomelo's migration builder extensions: whether a migration runs on MySQL, and dropping a primary key or unique
///     constraint after the table's foreign keys, which are added again after it. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlMigrationBuilderExtensions.cs
/// </summary>
public static class MySqlMigrationBuilderExtensions
{
    /// <summary>
    ///     Whether the migration runs on MySQL or a server that speaks it (MariaDB, SingleStore): its active provider is this
    ///     plugin, whose name every MySqlDialect gives the provider.
    /// </summary>
    public static bool IsMySql(this MigrationBuilder migrationBuilder)
        => string.Equals(migrationBuilder.ActiveProvider, typeof(MySqlDialect).Assembly.GetName().Name, StringComparison.Ordinal);

    /// <summary>
    ///     Drops the primary key; with <paramref name="recreateForeignKeys" />, the table's foreign keys are dropped first and
    ///     added again after, since MySQL may be using the key's index for one whose columns lead it, and then refuses to drop it.
    /// </summary>
    public static OperationBuilder<MySqlDropPrimaryKeyAndRecreateForeignKeysOperation> DropPrimaryKey(
        this MigrationBuilder migrationBuilder,
        string name,
        string table,
        string? schema = null,
        bool recreateForeignKeys = false)
    {
        var operation = new MySqlDropPrimaryKeyAndRecreateForeignKeysOperation
        {
            Name = name,
            Table = table,
            Schema = schema,
            RecreateForeignKeys = recreateForeignKeys
        };
        migrationBuilder.Operations.Add(operation);
        return new OperationBuilder<MySqlDropPrimaryKeyAndRecreateForeignKeysOperation>(operation);
    }

    /// <summary>
    ///     Drops the unique constraint; with <paramref name="recreateForeignKeys" />, the table's foreign keys are dropped first
    ///     and added again after, since MySQL may be using its index for one whose columns lead it.
    /// </summary>
    public static OperationBuilder<MySqlDropUniqueConstraintAndRecreateForeignKeysOperation> DropUniqueConstraint(
        this MigrationBuilder migrationBuilder,
        string name,
        string table,
        string? schema = null,
        bool recreateForeignKeys = false)
    {
        var operation = new MySqlDropUniqueConstraintAndRecreateForeignKeysOperation
        {
            Name = name,
            Table = table,
            Schema = schema,
            RecreateForeignKeys = recreateForeignKeys
        };
        migrationBuilder.Operations.Add(operation);
        return new OperationBuilder<MySqlDropUniqueConstraintAndRecreateForeignKeysOperation>(operation);
    }
}

/// <summary>A primary key dropped, with the table's foreign keys dropped before and added after when asked, as Pomelo's.</summary>
public class MySqlDropPrimaryKeyAndRecreateForeignKeysOperation : DropPrimaryKeyOperation
{
    /// <summary>Whether the table's foreign keys are dropped before the key and added again after.</summary>
    public virtual bool RecreateForeignKeys { get; set; }
}

/// <summary>A unique constraint dropped, with the table's foreign keys dropped before and added after when asked, as Pomelo's.</summary>
public class MySqlDropUniqueConstraintAndRecreateForeignKeysOperation : DropUniqueConstraintOperation
{
    /// <summary>Whether the table's foreign keys are dropped before the constraint and added again after.</summary>
    public virtual bool RecreateForeignKeys { get; set; }
}

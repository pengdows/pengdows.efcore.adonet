using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Creating and dropping databases has no portable ADO.NET or SQL form, so those operations are not supported;
///     <see cref="Exists" /> and <see cref="HasTables" /> work for any provider.
/// </summary>
public class AdoNetDatabaseCreator(
    RelationalDatabaseCreatorDependencies dependencies,
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    IAdoNetDialect dialect)
    : RelationalDatabaseCreator(dependencies)
{
    public override bool Exists()
    {
        try
        {
            Dependencies.Connection.Open(errorsExpected: true);
            Dependencies.Connection.Close();
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    public override bool HasTables()
        => Convert.ToInt64(
                rawSqlCommandBuilder
                    .Build(dialect.Sql.HasTablesSql)
                    .ExecuteScalar(
                        new RelationalCommandParameterObject(
                            Dependencies.Connection,
                            null,
                            null,
                            Dependencies.CurrentContext.Context,
                            Dependencies.CommandLogger,
                            CommandSource.Migrations)),
                CultureInfo.InvariantCulture)
            != 0;

    public override void Create()
        => throw new NotSupportedException($"Creating a database is not supported by the '{dialect.Name}' ADO.NET dialect.");

    public override void Delete()
        => throw new NotSupportedException($"Deleting a database is not supported by the '{dialect.Name}' ADO.NET dialect.");
}

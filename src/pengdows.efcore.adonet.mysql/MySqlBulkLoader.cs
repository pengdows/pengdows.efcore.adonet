using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     <see cref="MySqlBulkCopy" /> for MySqlConnector connections, which loads the rows with <c>LOAD DATA LOCAL INFILE</c>; multi-row
///     INSERT for any other driver. The copy enforces the table's constraints and fires its triggers, as an INSERT does.
/// </summary>
internal class MySqlBulkLoader(
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    ISqlGenerationHelper sqlGenerationHelper,
    IRelationalConnection connection,
    IRelationalCommandDiagnosticsLogger commandLogger,
    IAdoNetDialect dialect)
    : AdoNetInsertBulkLoader(rawSqlCommandBuilder, sqlGenerationHelper, connection, commandLogger, dialect)
{
    private readonly ISqlGenerationHelper _sqlGenerationHelper = sqlGenerationHelper;

    public override void Load(BulkLoad load)
    {
        if (!UsesNativeCopy(load))
        {
            base.Load(load);
            return;
        }

        var copy = Create(load);
        copy.WriteToServer(Rows(load));
    }

    public override async Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default)
    {
        if (!UsesNativeCopy(load))
        {
            await base.LoadAsync(load, cancellationToken).ConfigureAwait(false);
            return;
        }

        var copy = Create(load);
        await copy.WriteToServerAsync(Rows(load), cancellationToken).ConfigureAwait(false);
    }

    // LOAD DATA LOCAL lets the server ask the client for files, so the copy runs only where the connection string opts in
    // (AllowLoadLocalInfile=true). Otherwise the rows go as a multi-row INSERT, which needs no such option.
    private static bool UsesNativeCopy(BulkLoad load)
        => load.Columns.Count > 0 && UsesNativeCopyFor(load.Connection);

    internal static bool UsesNativeCopyFor(DbConnection connection)
        => connection is MySqlConnection mysql
            && new MySqlConnectionStringBuilder(mysql.ConnectionString).AllowLoadLocalInfile;

    private MySqlBulkCopy Create(BulkLoad load)
    {
        var connection = (MySqlConnection)load.Connection;
        var copy = new MySqlBulkCopy(connection, (MySqlConnector.MySqlTransaction)load.Transaction)
        {
            DestinationTableName = _sqlGenerationHelper.DelimitIdentifier(load.Table, load.Schema),
        };

        foreach (var column in load.Columns)
        {
            copy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(copy.ColumnMappings.Count, column.Name));
        }

        return copy;
    }

    private static DataTable Rows(BulkLoad load)
    {
        var table = new DataTable();
        foreach (var column in load.Columns)
        {
            var type = column.TypeMapping.Converter?.ProviderClrType ?? column.TypeMapping.ClrType;
            table.Columns.Add(column.Name, Nullable.GetUnderlyingType(type) ?? type);
        }

        foreach (var row in load.Rows)
        {
            table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        }

        return table;
    }
}

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary><see cref="SqlBulkCopy" /> for Microsoft.Data.SqlClient connections; multi-row INSERT for any other driver.</summary>
public class SqlServerBulkLoader(
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    ISqlGenerationHelper sqlGenerationHelper,
    IRelationalConnection connection,
    IRelationalCommandDiagnosticsLogger commandLogger,
    IDatabaseCapabilities capabilities)
    : AdoNetInsertBulkLoader(rawSqlCommandBuilder, sqlGenerationHelper, connection, commandLogger, capabilities)
{
    public override void Load(BulkLoad load)
    {
        if (load.Connection is not SqlConnection)
        {
            base.Load(load);
            return;
        }

        using var copy = Create(load);
        copy.WriteToServer(Rows(load));
    }

    public override async Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default)
    {
        if (load.Connection is not SqlConnection)
        {
            await base.LoadAsync(load, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var copy = Create(load);
        await copy.WriteToServerAsync(Rows(load), cancellationToken).ConfigureAwait(false);
    }

    private static SqlBulkCopy Create(BulkLoad load)
    {
        var copy = new SqlBulkCopy((SqlConnection)load.Connection, SqlBulkCopyOptions.Default, (SqlTransaction)load.Transaction)
        {
            DestinationTableName = load.Schema == null ? Bracket(load.Table) : Bracket(load.Schema) + "." + Bracket(load.Table),
            BulkCopyTimeout = 0
        };

        foreach (var column in load.Columns)
        {
            copy.ColumnMappings.Add(column.Name, column.Name);
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

    private static string Bracket(string name)
        => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
}

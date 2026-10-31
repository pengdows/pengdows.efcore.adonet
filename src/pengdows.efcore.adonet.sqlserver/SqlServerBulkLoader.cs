using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     <see cref="SqlBulkCopy" /> for Microsoft.Data.SqlClient connections; multi-row INSERT for any other driver. Like an
///     INSERT, the copy checks constraints and fires triggers, and it is bounded by the context's command timeout.
/// </summary>
internal class SqlServerBulkLoader(
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
        if (load.Connection is not SqlConnection || load.Columns.Count == 0)
        {
            base.Load(load);
            return;
        }

        using var copy = Create(load);
        foreach (var batch in Batches(load))
        {
            copy.WriteToServer(batch);
        }
    }

    public override async Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default)
    {
        if (load.Connection is not SqlConnection || load.Columns.Count == 0)
        {
            await base.LoadAsync(load, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var copy = Create(load);
        foreach (var batch in Batches(load))
        {
            await copy.WriteToServerAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private SqlBulkCopy Create(BulkLoad load)
    {
        var connection = (SqlConnection)load.Connection;
        var copy = new SqlBulkCopy(
            connection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers, (SqlTransaction)load.Transaction)
        {
            DestinationTableName = _sqlGenerationHelper.DelimitIdentifier(load.Table, load.Schema),
            BulkCopyTimeout = load.Context.Database.GetCommandTimeout() ?? connection.CommandTimeout
        };

        foreach (var column in load.Columns)
        {
            copy.ColumnMappings.Add(column.Name, column.Name);
        }

        return copy;
    }

    // Rows per table: the copy writes each table as it is built, so a large load holds one batch at a time, not every row.
    private const int BatchSize = 10_000;

    private static IEnumerable<DataTable> Batches(BulkLoad load)
    {
        for (var start = 0; start < load.Rows.Count; start += BatchSize)
        {
            yield return Table(load, start, Math.Min(BatchSize, load.Rows.Count - start));
        }
    }

    private static DataTable Table(BulkLoad load, int start, int count)
    {
        var table = new DataTable();
        foreach (var column in load.Columns)
        {
            var type = column.TypeMapping.Converter?.ProviderClrType ?? column.TypeMapping.ClrType;
            table.Columns.Add(column.Name, Nullable.GetUnderlyingType(type) ?? type);
        }

        for (var i = start; i < start + count; i++)
        {
            table.Rows.Add(load.Rows[i].Select(v => v ?? DBNull.Value).ToArray());
        }

        return table;
    }
}

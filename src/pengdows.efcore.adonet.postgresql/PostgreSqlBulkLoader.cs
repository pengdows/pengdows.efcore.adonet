using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>Binary <c>COPY ... FROM STDIN</c> for Npgsql connections; multi-row INSERT for any other driver.</summary>
public class PostgreSqlBulkLoader(
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    ISqlGenerationHelper sqlGenerationHelper,
    IRelationalConnection connection,
    IRelationalCommandDiagnosticsLogger commandLogger,
    IDatabaseCapabilities capabilities)
    : AdoNetInsertBulkLoader(rawSqlCommandBuilder, sqlGenerationHelper, connection, commandLogger, capabilities)
{
    private readonly ISqlGenerationHelper _sqlGenerationHelper = sqlGenerationHelper;

    public override void Load(BulkLoad load)
    {
        if (load.Connection is not NpgsqlConnection npgsql)
        {
            base.Load(load);
            return;
        }

        using var importer = npgsql.BeginBinaryImport(CopySql(load));
        foreach (var row in load.Rows)
        {
            importer.StartRow();
            foreach (var value in row)
            {
                if (value == null)
                {
                    importer.WriteNull();
                }
                else
                {
                    importer.Write(value);
                }
            }
        }

        importer.Complete();
    }

    public override async Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default)
    {
        if (load.Connection is not NpgsqlConnection npgsql)
        {
            await base.LoadAsync(load, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var importer = await npgsql.BeginBinaryImportAsync(CopySql(load), cancellationToken).ConfigureAwait(false);
        foreach (var row in load.Rows)
        {
            await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
            foreach (var value in row)
            {
                if (value == null)
                {
                    await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await importer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private string CopySql(BulkLoad load)
        => $"COPY {_sqlGenerationHelper.DelimitIdentifier(load.Table, load.Schema)} "
            + $"({string.Join(", ", load.Columns.Select(c => _sqlGenerationHelper.DelimitIdentifier(c.Name)))}) FROM STDIN (FORMAT BINARY)";
}

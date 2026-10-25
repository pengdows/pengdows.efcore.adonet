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
    IAdoNetDialect dialect)
    : AdoNetInsertBulkLoader(rawSqlCommandBuilder, sqlGenerationHelper, connection, commandLogger, dialect)
{
    private readonly ISqlGenerationHelper _sqlGenerationHelper = sqlGenerationHelper;

    public override void Load(BulkLoad load)
    {
        if (load.Connection is not NpgsqlConnection npgsql || load.Columns.Count == 0)
        {
            base.Load(load);
            return;
        }

        var dataTypeNames = DataTypeNames(load);
        using var importer = npgsql.BeginBinaryImport(CopySql(load));
        foreach (var row in load.Rows)
        {
            importer.StartRow();
            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] is not { } value)
                {
                    importer.WriteNull();
                    continue;
                }

                importer.Write(value, dataTypeNames[i]);
            }
        }

        importer.Complete();
    }

    public override async Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default)
    {
        if (load.Connection is not NpgsqlConnection npgsql || load.Columns.Count == 0)
        {
            await base.LoadAsync(load, cancellationToken).ConfigureAwait(false);
            return;
        }

        var dataTypeNames = DataTypeNames(load);
        await using var importer = await npgsql.BeginBinaryImportAsync(CopySql(load), cancellationToken).ConfigureAwait(false);
        foreach (var row in load.Rows)
        {
            await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] is not { } value)
                {
                    await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await importer.WriteAsync(value, dataTypeNames[i], cancellationToken).ConfigureAwait(false);
            }
        }

        await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    // Binary COPY has no conversions: a value must be sent as exactly the column's type, which the CLR type alone doesn't
    // tell (a DateTime may be bound for a date column). Npgsql names types as PostgreSQL does: lower case, no facets.
    private static string[] DataTypeNames(BulkLoad load)
        => load.Columns.Select(c => DataTypeName(c.TypeMapping.StoreType)).ToArray();

    private static string DataTypeName(string storeType)
    {
        var open = storeType.IndexOf('(', StringComparison.Ordinal);
        var name = open < 0 ? storeType : storeType[..open] + storeType[(storeType.IndexOf(')', open) + 1)..];
        return name.Trim().ToLowerInvariant();
    }

    private string CopySql(BulkLoad load)
        => $"COPY {_sqlGenerationHelper.DelimitIdentifier(load.Table, load.Schema)} "
            + $"({string.Join(", ", load.Columns.Select(c => _sqlGenerationHelper.DelimitIdentifier(c.Name)))}) FROM STDIN (FORMAT BINARY)";
}

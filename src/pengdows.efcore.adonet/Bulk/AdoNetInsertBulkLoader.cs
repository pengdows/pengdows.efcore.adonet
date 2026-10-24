using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Bulk;

/// <summary>
///     Multi-row <c>INSERT ... VALUES (...), (...)</c> (an SQL:1999 table value constructor), as many rows per command as
///     <see cref="IDatabaseCapabilities.MaxParametersPerCommand" /> allows, through EF's command pipeline so logging and
///     interceptors apply.
/// </summary>
public class AdoNetInsertBulkLoader(
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    ISqlGenerationHelper sqlGenerationHelper,
    IRelationalConnection connection,
    IRelationalCommandDiagnosticsLogger commandLogger,
    IDatabaseCapabilities capabilities) : IAdoNetBulkLoader
{
    /// <summary>The most rows one INSERT names, whatever the parameter limit (SQL Server's VALUES limit).</summary>
    protected virtual int MaxRowsPerCommand
        => 1000;

    public virtual void Load(BulkLoad load)
    {
        foreach (var (sql, parameters) in Commands(load))
        {
            var command = rawSqlCommandBuilder.Build(sql, parameters);
            command.RelationalCommand.ExecuteNonQuery(Parameters(load, command));
        }
    }

    public virtual async Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default)
    {
        foreach (var (sql, parameters) in Commands(load))
        {
            var command = rawSqlCommandBuilder.Build(sql, parameters);
            await command.RelationalCommand.ExecuteNonQueryAsync(Parameters(load, command), cancellationToken).ConfigureAwait(false);
        }
    }

    private IEnumerable<(string Sql, object[] Parameters)> Commands(BulkLoad load)
    {
        var table = sqlGenerationHelper.DelimitIdentifier(load.Table, load.Schema);
        if (load.Columns.Count == 0)
        {
            foreach (var _ in load.Rows)
            {
                yield return ($"INSERT INTO {table} DEFAULT VALUES", []);
            }

            yield break;
        }

        var columns = string.Join(", ", load.Columns.Select(c => sqlGenerationHelper.DelimitIdentifier(c.Name)));
        var rowsPerCommand = Math.Clamp(capabilities.MaxParametersPerCommand / load.Columns.Count, 1, MaxRowsPerCommand);

        foreach (var chunk in load.Rows.Chunk(rowsPerCommand))
        {
            using var dbCommand = load.Connection.CreateCommand();
            var parameters = new object[chunk.Length * load.Columns.Count];
            var values = new string[chunk.Length];
            for (var row = 0; row < chunk.Length; row++)
            {
                var placeholders = new string[load.Columns.Count];
                for (var column = 0; column < load.Columns.Count; column++)
                {
                    var index = row * load.Columns.Count + column;
                    var name = "p" + index;
                    placeholders[column] = sqlGenerationHelper.GenerateParameterNamePlaceholder(name);
                    parameters[index] = load.Columns[column].TypeMapping.CreateParameter(
                        dbCommand, sqlGenerationHelper.GenerateParameterName(name), chunk[row][column], nullable: true);
                }

                values[row] = "(" + string.Join(", ", placeholders) + ")";
            }

            yield return ($"INSERT INTO {table} ({columns})\nVALUES {string.Join(",\n", values)}", parameters);
        }
    }

    private RelationalCommandParameterObject Parameters(BulkLoad load, RawSqlCommand command)
        => new(connection, command.ParameterValues, null, load.Context, commandLogger, CommandSource.SaveChanges);
}

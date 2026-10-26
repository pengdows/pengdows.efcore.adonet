using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     With <c>ADONET_SPEC_SQL_LOG</c> set to a file path, every command a test store's contexts run is appended there, failed
///     ones too: EF's query assertions don't show the SQL of a query the database rejected.
/// </summary>
internal sealed class SqlLog : DbCommandInterceptor
{
    private static readonly string? Path = Environment.GetEnvironmentVariable("ADONET_SPEC_SQL_LOG");
    private static readonly Lock Gate = new();
    private static readonly SqlLog Instance = new();

    public static DbContextOptionsBuilder AddTo(DbContextOptionsBuilder builder)
        => Path == null ? builder : builder.AddInterceptors(Instance);

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
        => Write(command, eventData.Exception.Message);

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Write(command, eventData.Exception.Message);
        return Task.CompletedTask;
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Write(command, null);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Write(command, null);
        return ValueTask.FromResult(result);
    }

    private static void Write(DbCommand command, string? error)
    {
        lock (Gate)
        {
            File.AppendAllText(
                Path!,
                (error == null ? "--\n" : $"-- FAILED: {error}\n")
                + string.Concat(command.Parameters.Cast<DbParameter>().Select(p => $"-- {p.ParameterName} = {p.Value}\n"))
                + command.CommandText + "\n\n");
        }
    }
}

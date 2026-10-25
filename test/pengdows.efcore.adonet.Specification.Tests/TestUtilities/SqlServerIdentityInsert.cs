using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     Lets the specification fixtures seed SQL Server's identity columns with their own keys, as the other databases
///     allow: an insert naming an identity column runs between <c>SET IDENTITY_INSERT ... ON</c> and <c>OFF</c>, guarded the
///     way EF's SQL Server migrations guard seed data. Microsoft's suites seed Northwind from a script for the same reason.
///     Test infrastructure only: the provider, like Microsoft's, refuses such inserts. Needs one insert per command
///     (<c>MaxBatchSize(1)</c>), since only one table per session can have <c>IDENTITY_INSERT</c> on.
/// </summary>
public sealed partial class SqlServerIdentityInsert : DbCommandInterceptor
{
    public static readonly SqlServerIdentityInsert Instance = new();

    [GeneratedRegex(@"^\s*INSERT\s+INTO\s+(?<table>(?:\[[^\]]+\]\.)?\[[^\]]+\])\s*\((?<columns>[^)]*)\)", RegexOptions.IgnoreCase)]
    private static partial Regex Insert();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Wrap(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Wrap(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Wrap(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Wrap(command);
        return ValueTask.FromResult(result);
    }

    private static void Wrap(DbCommand command)
    {
        if (Insert().Match(command.CommandText) is not { Success: true } insert)
        {
            return;
        }

        var table = insert.Groups["table"].Value;
        var columns = string.Join(
            ", ", insert.Groups["columns"].Value.Split(',').Select(c => "N'" + c.Trim().Trim('[', ']').Replace("'", "''") + "'"));
        var hasIdentity = $"IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN ({columns}) AND [object_id] = OBJECT_ID(N'{table.Replace("'", "''")}'))";
        command.CommandText = $"{hasIdentity} SET IDENTITY_INSERT {table} ON;\n{command.CommandText}\n;{hasIdentity} SET IDENTITY_INSERT {table} OFF;";
    }
}

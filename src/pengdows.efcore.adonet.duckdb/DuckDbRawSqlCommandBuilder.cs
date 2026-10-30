using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.duckdb;

/// <summary>
///     EF's raw SQL writes each <c>{n}</c> placeholder as the parameter's bare name (<c>p0</c>), and DuckDB reads a bare name as a
///     column. This builder wraps each placeholder in control-character sentinels before EF substitutes it, and
///     <see cref="DuckDbRawSqlInterceptor" /> turns each sentinel-wrapped name into a <c>$</c> reference just before the command
///     runs. Parameters keep their bare names, which DuckDB.NET binds. Only raw SQL carries sentinels.
/// </summary>
internal sealed class DuckDbRawSqlCommandBuilder(IRawSqlCommandBuilder inner) : IRawSqlCommandBuilder
{
    internal const char Open = '\u0001';
    internal const char Close = '\u0002';

    // EF's implementation is internal: a decorator needs an instance of it to wrap. ActivatorUtilities resolves its
    // dependencies from the service provider; a missing type fails at first use with a message naming it.
    internal static readonly Type InnerType = typeof(IRawSqlCommandBuilder).Assembly.GetType(
        "Microsoft.EntityFrameworkCore.Storage.Internal.RawSqlCommandBuilder", throwOnError: true)!;

    public IRelationalCommand Build(string sql)
        => inner.Build(sql);

    public RawSqlCommand Build(string sql, IEnumerable<object?> parameters)
        => inner.Build(Mark(sql), parameters);

    public RawSqlCommand Build(string sql, IEnumerable<object?> parameters, IModel model)
        => inner.Build(Mark(sql), parameters, model);

    // Wraps each unescaped {n} in the sentinels; {{ and }} are EF's escapes for literal braces and pass through.
    internal static string Mark(string sql)
    {
        var text = new System.Text.StringBuilder(sql.Length + 8);
        for (var i = 0; i < sql.Length; i++)
        {
            if (sql[i] is '{' or '}' && i + 1 < sql.Length && sql[i + 1] == sql[i])
            {
                text.Append(sql[i], 2);
                i++;
            }
            else if (PlaceholderEnd(sql, i) is { } end)
            {
                text.Append(Open).Append(sql, i, end - i + 1).Append(Close);
                i = end;
            }
            else
            {
                text.Append(sql[i]);
            }
        }

        return text.ToString();
    }

    // The index of the } closing a placeholder that starts at i, when the text there is {digits}.
    private static int? PlaceholderEnd(string sql, int i)
    {
        var end = i + 1;
        while (end < sql.Length && char.IsAsciiDigit(sql[end]))
        {
            end++;
        }

        return end > i + 1 && end < sql.Length && sql[end] == '}' ? end : null;
    }
}

/// <summary>Replaces each sentinel-wrapped parameter name with its <c>$</c> reference, and strips the sentinels from any other text.</summary>
internal sealed class DuckDbRawSqlInterceptor : DbCommandInterceptor
{
    private static readonly Regex Sentinel = new($"{DuckDbRawSqlCommandBuilder.Open}([^{DuckDbRawSqlCommandBuilder.Close}]*){DuckDbRawSqlCommandBuilder.Close}");

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Restore(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Restore(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Restore(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Restore(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Restore(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Restore(command);
        return ValueTask.FromResult(result);
    }

    private static void Restore(DbCommand command)
    {
        if (command.CommandText.IndexOf(DuckDbRawSqlCommandBuilder.Open) >= 0)
        {
            command.CommandText = Sentinel.Replace(command.CommandText, "$$$1");
        }
    }
}

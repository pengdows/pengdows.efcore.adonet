using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     A fakeDb connection emulating SQLite, handed to EF as an external connection, so a test scripts the rows a statement
///     returns and reads back what was sent from EF's own command log.
/// </summary>
public sealed class FakeSqlite
{
    private readonly List<string> _commands = [];
    private readonly List<string> _parameters = [];

    public fakeDbConnection Connection { get; } = new() { ConnectionString = "Data Source=fake;EmulatedProduct=Sqlite" };

    /// <summary>The SQL of each executed command, in order.</summary>
    public IReadOnlyList<string> Commands
        => _commands;

    /// <summary>Each executed command's parameters as EF logs them, e.g. <c>@p='x' (Size = 1)</c>.</summary>
    public IReadOnlyList<string> Parameters
        => _parameters;

    public TContext Context<TContext>(Func<DbContextOptions<TContext>, TContext> create, Action<AdoNetDbContextOptionsBuilder>? adoNet = null)
        where TContext : DbContext
        => create(new DbContextOptionsBuilder<TContext>()
            .UseAdoNetSqlite(Connection, contextOwnsConnection: false, adoNet)
            .EnableSensitiveDataLogging()
            .LogTo(Record, [RelationalEventId.CommandExecuted])
            .Options);

    /// <summary>The next reader returns these rows, each a row's values in column order.</summary>
    public void Returns(params object?[][] rows)
        => Connection.EnqueueReaderResult(rows.Select(r => r.Select((v, i) => (Name: "c" + i, Value: v)).ToDictionary(c => c.Name, c => c.Value)).ToList());

    // "info: ... CommandExecuted ...\n      Executed DbCommand (1ms) [Parameters=[...], CommandType=...]\n      SQL..."
    private void Record(string message)
    {
        var lines = message.ReplaceLineEndings("\n").Split('\n');
        var header = lines[1];
        var start = header.IndexOf("[Parameters=[", StringComparison.Ordinal) + "[Parameters=[".Length;
        _parameters.Add(header[start..header.IndexOf("], CommandType=", StringComparison.Ordinal)]);
        _commands.Add(string.Join("\n", lines.Skip(2).Select(l => l.Length >= 6 ? l[6..] : l.TrimStart())).TrimEnd());
    }
}

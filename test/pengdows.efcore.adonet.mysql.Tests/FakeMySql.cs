using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     One fakeDb connection emulating MySQL, handed to EF as an external connection, so a test runs a query or a migration
///     through it, scripts the rows it reads back, and reads what was sent from EF's own command log (with parameter values),
///     which doesn't depend on the driver. As the core tests' FakeDatabase.
/// </summary>
public sealed class FakeMySql
{
    private readonly List<string> _commands = [];
    private readonly List<string> _parameters = [];

    public fakeDbConnection Connection { get; } = new() { ConnectionString = "Data Source=fake;EmulatedProduct=MySql" };

    /// <summary>The SQL of each executed command, in order.</summary>
    public IReadOnlyList<string> Commands
        => _commands;

    /// <summary>Each executed command's parameter list as EF logs it, e.g. <c>@p0='1', @p1='x' (Size = 4000)</c>.</summary>
    public IReadOnlyList<string> Parameters
        => _parameters;

    public TContext Context<TContext>(
        Func<DbContextOptions<TContext>, TContext> create,
        bool mariaDb = false,
        Action<AdoNetDbContextOptionsBuilder>? adoNet = null)
        where TContext : DbContext
        => create(new DbContextOptionsBuilder<TContext>()
            .UseAdoNetMySql(Connection, contextOwnsConnection: false, o =>
            {
                o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance);
                adoNet?.Invoke(o);
            })
            .EnableSensitiveDataLogging()
            .LogTo(Record, [RelationalEventId.CommandExecuted])
            .Options);

    /// <summary>The next reader returns these rows, each a column name and value in the order the query selects them.</summary>
    public void Returns(params (string Column, object? Value)[][] rows)
        => Connection.EnqueueReaderResult(rows.Select(r => r.ToDictionary(c => c.Column, c => c.Value)).ToList());

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

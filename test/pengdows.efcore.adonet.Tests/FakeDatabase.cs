using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     One fakeDb connection, handed to EF as an external connection so tests script its results and failures.
///     What ran is read from EF's own command log, which doesn't depend on the driver.
/// </summary>
public sealed class FakeDatabase
{
    private readonly List<string> _commands = [];
    private readonly List<string> _parameters = [];

    public fakeDbConnection Connection { get; } = new() { ConnectionString = $"Data Source=fake;EmulatedProduct={SupportedDatabase.PostgreSql}" };

    /// <summary>The SQL of each executed command, in order.</summary>
    public IReadOnlyList<string> Commands
        => _commands;

    /// <summary>Each executed command's parameter list as EF logs it, e.g. <c>@p0='1', @p1='x' (Size = 4000)</c>.</summary>
    public IReadOnlyList<string> Parameters
        => _parameters;

    public ItemContext Context(IAdoNetDialect dialect, Action<AdoNetDbContextOptionsBuilder>? adoNet = null)
        => ContextOf<ItemContext>(dialect, o => new ItemContext(o), adoNet);

    public TContext ContextOf<TContext>(
        IAdoNetDialect dialect,
        Func<DbContextOptions<TContext>, TContext> create,
        Action<AdoNetDbContextOptionsBuilder>? adoNet = null)
        where TContext : DbContext
        => create(new DbContextOptionsBuilder<TContext>()
            .UseAdoNet(Connection, contextOwnsConnection: false, o =>
            {
                o.UseDialect(dialect);
                adoNet?.Invoke(o);
            })
            .EnableSensitiveDataLogging()
            .AllowManyDialects()
            .LogTo(Record, [RelationalEventId.CommandExecuted])
            .Options);

    // "info: ... CommandExecuted ...\n      Executed DbCommand (1ms) [Parameters=[...], CommandType=...]\n      SQL..."
    private void Record(string message)
    {
        var lines = message.ReplaceLineEndings("\n").Split('\n');
        var header = lines[1];
        var start = header.IndexOf("[Parameters=[", StringComparison.Ordinal) + "[Parameters=[".Length;
        _parameters.Add(header[start..header.IndexOf("], CommandType=", StringComparison.Ordinal)]);
        _commands.Add(string.Join("\n", lines.Skip(2).Select(l => l.Length >= 6 ? l[6..] : l.TrimStart())).TrimEnd());
    }

    /// <summary>The next reader returns these rows.</summary>
    public void Returns(params Dictionary<string, object?>[] rows)
        => Connection.EnqueueReaderResult(rows);

    public static Dictionary<string, object?> Row(string column, object? value)
        => new() { [column] = value };
}

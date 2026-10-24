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
    private readonly List<string> _log = [];

    public fakeDbConnection Connection { get; } = new() { ConnectionString = $"Data Source=fake;EmulatedProduct={SupportedDatabase.PostgreSql}" };

    /// <summary>Executed commands, each as EF logs it: parameters, then the SQL.</summary>
    public IReadOnlyList<string> Commands
        => _log;

    public string Sql
        => string.Join("\n", _log);

    public ItemContext Context(IAdoNetDialect dialect, Action<AdoNetDbContextOptionsBuilder>? adoNet = null)
        => new(new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(Connection, contextOwnsConnection: false, o =>
            {
                o.UseDialect(dialect);
                adoNet?.Invoke(o);
            })
            .EnableSensitiveDataLogging()
            .LogTo(m => _log.Add(m.ReplaceLineEndings("\n")), [RelationalEventId.CommandExecuted])
            .Options);

    /// <summary>The next reader returns these rows.</summary>
    public void Returns(params Dictionary<string, object?>[] rows)
        => Connection.EnqueueReaderResult(rows);

    public static Dictionary<string, object?> Row(string column, object? value)
        => new() { [column] = value };
}

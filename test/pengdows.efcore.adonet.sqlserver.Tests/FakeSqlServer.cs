using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>A fakeDb connection emulating SQL Server for provider-specific command and materialization tests.</summary>
public sealed class FakeSqlServer
{
    private readonly List<string> _commands = [];
    private readonly List<string> _parameters = [];

    public fakeDbConnection Connection { get; } = new() { ConnectionString = "Data Source=fake;EmulatedProduct=SqlServer" };

    public IReadOnlyList<string> Commands => _commands;

    public IReadOnlyList<string> Parameters => _parameters;

    public TContext Context<TContext>(Func<DbContextOptions<TContext>, TContext> create, SqlServerDialect? dialect = null)
        where TContext : DbContext
        => create(new DbContextOptionsBuilder<TContext>()
            .UseAdoNetSqlServer(Connection, contextOwnsConnection: false, o => o.UseDialect(dialect ?? SqlServerDialect.Instance))
            .EnableSensitiveDataLogging()
            .LogTo(Record, [RelationalEventId.CommandExecuted])
            .Options);

    public void Returns(params object?[][] rows)
        => Connection.EnqueueReaderResult(rows.Select(r => r.Select((value, index) => (Name: "c" + index, Value: value))
            .ToDictionary(c => c.Name, c => c.Value)).ToList());

    private void Record(string message)
    {
        var lines = message.ReplaceLineEndings("\n").Split('\n');
        var header = lines[1];
        var start = header.IndexOf("[Parameters=[", StringComparison.Ordinal) + "[Parameters=[".Length;
        _parameters.Add(header[start..header.IndexOf("], CommandType=", StringComparison.Ordinal)]);
        _commands.Add(string.Join("\n", lines.Skip(2).Select(line => line.Length >= 6 ? line[6..] : line.TrimStart())).TrimEnd());
    }
}

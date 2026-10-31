using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

public class SessionSetupTests
{
    private const string Setup = "SET SESSION mode = 'iso'";
    private static readonly IAdoNetDialect Dialect = new PartsDialect("Session setup", new PartsSql(sessionSetup: Setup));

    private readonly FakeDatabase _db = new();

    private int Setups
        => _db.Connection.ExecutedNonQueryTexts.Count(t => t == Setup);

    [Fact]
    public void Each_session_EF_opens_is_set_up_before_its_first_command()
    {
        using var context = _db.Context(Dialect);

        _ = context.Items.ToList();
        _ = context.Items.ToList();

        // EF opens and closes the connection around each query: two sessions.
        Assert.Equal(2, Setups);
    }

    [Fact]
    public async Task A_session_the_application_opened_is_set_up_once_when_EF_first_uses_it()
    {
        await _db.Connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = _db.Context(Dialect);

        _ = await context.Items.ToListAsync(TestContext.Current.CancellationToken);
        _ = await context.Items.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, Setups);
    }

    [Fact]
    public void Closing_ends_the_session_so_a_reopened_connection_is_set_up_again()
    {
        using var context = _db.Context(Dialect);
        _db.Connection.Open();
        _ = context.Items.ToList();
        _db.Connection.Close();

        _db.Connection.Open();
        _ = context.Items.ToList();

        Assert.Equal(2, Setups);
    }

    [Fact]
    public void A_dialect_without_setup_runs_nothing_extra()
    {
        using var context = _db.Context(AdoNetDialect.Ansi);

        _ = context.Items.ToList();

        Assert.Empty(_db.Connection.ExecutedNonQueryTexts);
    }

    // Two contexts with different dialects share one connection: each one's setup runs on the sessions it opens, rather than
    // the first context's setup being kept for the connection's lifetime.
    [Fact]
    public void Contexts_sharing_a_connection_each_get_their_own_dialects_setup()
    {
        const string otherSetup = "SET SESSION mode = 'other'";
        var other = new PartsDialect("Other setup", new PartsSql(sessionSetup: otherSetup));
        using var first = _db.Context(Dialect);
        using var second = _db.Context(other);

        _ = first.Items.ToList();
        _ = second.Items.ToList();

        Assert.Equal(1, Setups);
        Assert.Equal(1, _db.Connection.ExecutedNonQueryTexts.Count(t => t == otherSetup));
    }

    // A driver that refuses synchronous execution: its session setup must still run when EF opens the connection
    // asynchronously, which only an asynchronous execution can do.
    [Fact]
    public async Task A_session_opened_asynchronously_is_set_up_without_synchronous_io()
    {
        var connection = new SyncRejectingConnection { ConnectionString = "Data Source=fake;EmulatedProduct=PostgreSql" };
        var options = new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(Dialect))
            .AllowManyDialects()
            .Options;
        await using var context = new ItemContext(options);

        _ = await context.Items.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, connection.ExecutedNonQueryTexts.Count(t => t == Setup));
    }

    private sealed class SyncRejectingConnection : pengdows.crud.fakeDb.fakeDbConnection
    {
        protected override System.Data.Common.DbCommand CreateDbCommand()
            => new SyncRejectingCommand(this);
    }

    // fakeDb's asynchronous execution runs its synchronous one inside the call, so the flag tells the two apart.
    private sealed class SyncRejectingCommand(System.Data.Common.DbConnection connection)
        : pengdows.crud.fakeDb.fakeDbCommand(connection)
    {
        private bool _asynchronous;

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            _asynchronous = true;
            try
            {
                return base.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                _asynchronous = false;
            }
        }

        public override int ExecuteNonQuery()
            => _asynchronous
                ? base.ExecuteNonQuery()
                : throw new InvalidOperationException("This driver rejects synchronous execution.");
    }
}

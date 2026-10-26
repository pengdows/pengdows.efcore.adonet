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
}

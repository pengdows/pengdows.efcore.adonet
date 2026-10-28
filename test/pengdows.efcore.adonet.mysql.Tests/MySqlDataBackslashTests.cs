using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Oracle's MySql.Data writes parameters into the statement text itself, escaping them for the session's sql_mode. With
///     the dialect's NO_BACKSLASH_ESCAPES session, quotes and backslashes must still round-trip through parameters and match
///     the dialect's own literals. pengdows.crud saw O'Brien stored as O\'Brien in this mode.
///     Provider-only: the escaping is MySql.Data's.
/// </summary>
public class MySqlDataBackslashTests(MySqlDataFixture fixture) : IClassFixture<MySqlDataFixture>, IAsyncLifetime
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
        => await fixture.ResetAsync();

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private BlogContext Context()
        => new(new DbContextOptionsBuilder<BlogContext>()
            .UseAdoNet(fixture.Factory, fixture.ConnectionString, MySqlDialect.NoBackslashEscapes)
            .Options);

    [Theory]
    [InlineData("O'Brien")]
    [InlineData(@"back\slash")]
    [InlineData(@"both\'' and \\ "" quotes")]
    public async Task Quotes_and_backslashes_round_trip_through_parameters_and_literals(string name)
    {
        await using (var context = Context())
        {
            context.Add(new Blog { Name = name, CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Context())
        {
            Assert.Equal(name, await context.Blogs.Select(b => b.Name).SingleAsync(Ct));
            Assert.Equal(1, await context.Blogs.CountAsync(b => b.Name == name, Ct));
            Assert.Equal(1, await context.Blogs.CountAsync(b => b.Name == EF.Constant(name), Ct));
        }
    }
}

using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     EF.Functions.ILike, as Npgsql's provider has it: PostgreSQL's case-insensitive ILIKE, with an escape character when
///     given, on the server. Provider-only: ILIKE is PostgreSQL's. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlDbFunctionsExtensions.cs
/// </summary>
public class PostgreSqlILikeTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        await using var context = fixture.CreateContext();
        foreach (var name in new[] { "Alpha", "ALPHABET", "beta", "100% sure" })
        {
            context.Add(new Blog { Name = name, CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() });
        }

        await context.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task ILike_matches_without_regard_to_case_on_the_server()
    {
        await using var context = fixture.CreateContext();
        var query = context.Blogs.Where(b => EF.Functions.ILike(b.Name, "alpha%")).Select(b => b.Name).OrderBy(n => n);

        Assert.Contains(" ILIKE ", query.ToQueryString());
        Assert.Equal(["ALPHABET", "Alpha"], await query.ToListAsync(Ct));
    }

    [Fact]
    public async Task ILike_takes_an_escape_character()
    {
        await using var context = fixture.CreateContext();

        var names = await context.Blogs.Where(b => EF.Functions.ILike(b.Name, "100!% SURE", "!")).Select(b => b.Name).ToListAsync(Ct);

        Assert.Equal(["100% sure"], names);
    }
}

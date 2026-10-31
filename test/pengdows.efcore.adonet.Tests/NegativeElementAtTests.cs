using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     ElementAt with a negative position throws ArgumentOutOfRangeException, as LINQ does, on every provider. The sync
///     path gets it from System.Linq; the async path goes straight to the query pipeline, so the provider checks it there.
/// </summary>
public class NegativeElementAtTests
{
    private readonly FakeDatabase _db = new();

    [Fact]
    public async Task ElementAtAsync_with_a_negative_constant_throws_ArgumentOutOfRange()
    {
        await using var context = _db.ContextOf<ItemContext>(new PartsDialect("Items"), o => new ItemContext(o));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            context.Items.OrderBy(i => i.Id).ElementAtAsync(-1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ElementAtAsync_with_a_negative_captured_variable_throws_ArgumentOutOfRange()
    {
        await using var context = _db.ContextOf<ItemContext>(new PartsDialect("Items"), o => new ItemContext(o));
        var position = -2;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            context.Items.OrderBy(i => i.Id).ElementAtAsync(position, TestContext.Current.CancellationToken));
    }

    public sealed class Item
    {
        public int Id { get; set; }
    }

    public sealed class ItemContext(DbContextOptions<ItemContext> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
    }
}

using System.Data;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A provider states the isolation levels its engine can honour, and a transaction asking for any other level is
///     refused with a NotSupportedException that names the level. Nothing is silently remapped to a weaker or different level.
/// </summary>
public class IsolationRefusalTests
{
    private static readonly IAdoNetDialect SnapshotOnly = new PartsDialect("SnapshotOnly", sql: new SnapshotOnlySql());

    private readonly FakeDatabase _db = new();

    [Fact]
    public void A_level_the_dialect_does_not_support_is_refused_by_name()
    {
        using var context = _db.ContextOf<IsolationContext>(SnapshotOnly, o => new IsolationContext(o));

        var ex = Assert.Throws<NotSupportedException>(() => context.Database.BeginTransaction(IsolationLevel.Serializable));

        Assert.Contains("Serializable", ex.Message);
    }

    [Fact]
    public async Task A_level_the_dialect_does_not_support_is_refused_asynchronously()
    {
        await using var context = _db.ContextOf<IsolationContext>(SnapshotOnly, o => new IsolationContext(o));

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            context.Database.BeginTransactionAsync(IsolationLevel.Serializable, TestContext.Current.CancellationToken));

        Assert.Contains("Serializable", ex.Message);
    }

    [Fact]
    public void A_supported_level_is_passed_through()
    {
        using var context = _db.ContextOf<IsolationContext>(SnapshotOnly, o => new IsolationContext(o));

        using var transaction = context.Database.BeginTransaction(IsolationLevel.Snapshot);

        Assert.NotNull(transaction);
    }

    private sealed class SnapshotOnlySql : AnsiSqlDialect
    {
        public override IReadOnlySet<System.Data.IsolationLevel> SupportedIsolationLevels { get; } =
            new HashSet<System.Data.IsolationLevel> { System.Data.IsolationLevel.Unspecified, System.Data.IsolationLevel.Snapshot };
    }

    public sealed class IsolationContext(DbContextOptions<IsolationContext> options) : DbContext(options);
}

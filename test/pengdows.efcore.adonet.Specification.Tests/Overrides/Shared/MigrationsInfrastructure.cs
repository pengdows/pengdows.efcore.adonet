using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class MigrationsInfrastructureAdoNetTest<THost>
{
    // EF 10.0.11's test begins the application's transaction without awaiting it, so the begin races the migrator's own and
    // a driver whose begin does I/O asynchronously sometimes loses ("already in a transaction"). EF's main awaits it.
    // Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/f249a6e0b95df6a1bf37d06885d0b8b06bb62de8/test/EFCore.Relational.Specification.Tests/Migrations/MigrationsInfrastructureTestBase.cs
    public override async Task Can_apply_two_migrations_in_transaction_async()
    {
        using var db = Fixture.CreateContext();
        await db.Database.EnsureDeletedAsync();
        await GiveMeSomeTimeAsync(db);
        await db.GetService<IRelationalDatabaseCreator>().CreateAsync();

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("Migration1");
            await migrator.MigrateAsync("Migration2");

            var history = db.GetService<IHistoryRepository>();
            Assert.Collection(
                await history.GetAppliedMigrationsAsync(),
                x => Assert.Equal("00000000000001_Migration1", x.MigrationId),
                x => Assert.Equal("00000000000002_Migration2", x.MigrationId));
        });

        Assert.Equal(
            LogLevel.Warning,
            Fixture.TestSqlLoggerFactory.Log.First(l => l.Id == RelationalEventId.MigrationsUserTransactionWarning).Level);
    }
}

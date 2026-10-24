using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.adonet.Migrations;

/// <summary>Migrations are out of scope for this provider; EF requires the service to exist.</summary>
public class AdoNetHistoryRepository(HistoryRepositoryDependencies dependencies) : HistoryRepository(dependencies)
{
    private static NotSupportedException NotSupported()
        => new("Migrations are not supported by the ADO.NET provider. Manage the schema outside EF Core.");

    public override LockReleaseBehavior LockReleaseBehavior
        => throw NotSupported();

    protected override string ExistsSql
        => throw NotSupported();

    protected override bool InterpretExistsResult(object? value)
        => throw NotSupported();

    public override string GetCreateIfNotExistsScript()
        => throw NotSupported();

    public override string GetBeginIfNotExistsScript(string migrationId)
        => throw NotSupported();

    public override string GetBeginIfExistsScript(string migrationId)
        => throw NotSupported();

    public override string GetEndIfScript()
        => throw NotSupported();

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
        => throw NotSupported();

    public override Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
        => throw NotSupported();
}

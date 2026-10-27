using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests.MySql;

public partial class StoreValueGenerationMySqlTest
{
    protected override bool ShouldCreateImplicitTransaction(
        EntityState firstOperationType,
        EntityState? secondOperationType,
        GeneratedValues generatedValues,
        bool withSameEntityType)
        => generatedValues != GeneratedValues.None && firstOperationType != EntityState.Deleted
            || secondOperationType is not null
                && !(firstOperationType == secondOperationType
                    && firstOperationType == EntityState.Added
                    && withSameEntityType);

    protected override int ShouldExecuteInNumberOfCommands(
        EntityState firstOperationType,
        EntityState? secondOperationType,
        GeneratedValues generatedValues,
        bool withDatabaseGenerated)
        => 1;
}

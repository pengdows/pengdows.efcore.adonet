using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests.MariaDb;

public partial class StoreValueGenerationMariaDbTest
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

    private const string MySqlSameTypeBatchTransactionGap
        = "[SKIP:MariaDB] MySqlConnector/EF's multi-row same-type insert batch retains an implicit transaction even when no database-generated values are involved; the data operation is covered, but the upstream transaction-log assertion is not portable.";

    [ConditionalTheory(Skip = MySqlSameTypeBatchTransactionGap)]
    [MemberData(nameof(IsAsyncData))]
    public override Task Add_Add_with_same_entity_type_and_no_generated_values(bool async)
        => base.Add_Add_with_same_entity_type_and_no_generated_values(async);
}

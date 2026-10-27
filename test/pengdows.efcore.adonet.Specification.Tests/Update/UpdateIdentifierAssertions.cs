using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.TestModels.UpdatesModel;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Specification.Tests;

internal static class UpdateIdentifierAssertions
{
    public static void Assert<THost>(DbContext context)
        where THost : IDatabaseHost, new()
    {
        var entityType = context.Model.FindEntityType(
            typeof(LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly))!;
        var maxLength = new THost().Dialect.Capabilities.MaxIdentifierLength;

        AssertIdentifier(entityType.GetTableName(), maxLength);
        AssertIdentifier(entityType.FindPrimaryKey()!.GetName(), maxLength);
        AssertIdentifier(entityType.GetForeignKeys().Single().GetConstraintName(), maxLength);
        AssertIdentifier(entityType.GetIndexes().Single().GetDatabaseName(), maxLength);
    }

    private static void AssertIdentifier(string? identifier, int maxLength)
    {
        Xunit.Assert.NotNull(identifier);
        Xunit.Assert.InRange(identifier!.Length, 1, maxLength);
    }
}

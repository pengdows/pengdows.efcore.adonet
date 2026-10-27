using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Update;

namespace pengdows.efcore.adonet.Specification.Tests;

internal static class UpdateSqlGeneratorSupport
{
    public static IUpdateSqlGenerator Create<THost>(TestHelpers testHelpers)
        where THost : IDatabaseHost, new()
    {
        var model = testHelpers.CreateConventionBuilder().Model.FinalizeModel();
        var services = testHelpers.CreateContextServices(model);
        return new AdoNetUpdateSqlGenerator(
            new UpdateSqlGeneratorDependencies(
                services.GetRequiredService<ISqlGenerationHelper>(),
                services.GetRequiredService<IRelationalTypeMappingSource>()),
            new THost().Dialect);
    }
}

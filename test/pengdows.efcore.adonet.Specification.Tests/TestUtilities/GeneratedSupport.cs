using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>A driver's own parameter object, for EF's raw-SQL tests, which pass parameters as DbParameter.</summary>
public static class DbParameters
{
    public static DbParameter Create(IDatabaseHost host, string name, object value)
    {
        using var connection = host.CreateConnection(host.ConnectionStringFor("ef_parameters"));
        using var command = connection.CreateCommand();
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }
}

/// <summary>Sets, or removes, the query splitting behavior on the options.</summary>
public static class QuerySplitting
{
    public static DbContextOptionsBuilder Set(DbContextOptionsBuilder optionsBuilder, QuerySplittingBehavior behavior)
    {
        new AdoNetDbContextOptionsBuilder(optionsBuilder).UseQuerySplittingBehavior(behavior);
        return optionsBuilder;
    }

    public static DbContextOptionsBuilder Clear(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.Options.FindExtension<AdoNetOptionsExtension>() is { } extension)
        {
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
                (AdoNetOptionsExtension)extension.WithUseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery));
        }

        return optionsBuilder;
    }
}

/// <summary>Sets how a collection parameter is sent: as a parameter, as constants, or as several parameters.</summary>
public static class ParameterizedCollections
{
    public static DbContextOptionsBuilder Set(DbContextOptionsBuilder optionsBuilder, ParameterTranslationMode mode)
    {
        new AdoNetDbContextOptionsBuilder(optionsBuilder).UseParameterizedCollectionMode(mode);
        return optionsBuilder;
    }
}

/// <summary>Concrete fixture used by EF's relational model-builder tests.</summary>
public sealed class AdoNetRelationalModelBuilderFixture<THost>
    : global::Microsoft.EntityFrameworkCore.ModelBuilding.RelationalModelBuilderTest.RelationalModelBuilderFixture
    where THost : IDatabaseHost, new()
{
    public override global::Microsoft.EntityFrameworkCore.TestUtilities.TestHelpers TestHelpers
        => AdoNetTestHelpers<THost>.Instance;
}

/// <summary>Concrete shared-store fixture used by EF's interception tests.</summary>
public sealed class AdoNetInterceptionFixture<THost>
    : global::Microsoft.EntityFrameworkCore.InterceptionTestBase.InterceptionFixtureBase
    where THost : IDatabaseHost, new()
{
    protected override bool ShouldSubscribeToDiagnosticListener
        => false;

    protected override string StoreName
        => "Interception";

    protected override global::Microsoft.EntityFrameworkCore.TestUtilities.ITestStoreFactory TestStoreFactory
        => AdoNetTestStoreFactory<THost>.Instance;

    protected override IServiceCollection InjectInterceptors(
        IServiceCollection serviceCollection,
        IEnumerable<IInterceptor> injectedInterceptors)
        => base.InjectInterceptors(
            serviceCollection.AddEntityFrameworkAdoNet(new THost().Dialect),
            injectedInterceptors);
}

/// <summary>
///     A connection that fails to open, for EF's connection-interception tests: fakeDb's, not a hand-written one (REVIEW_POLICY:
///     fakeDb or a real provider only). Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.SqlServer.FunctionalTests/ConnectionInterceptionSqlServerTest.cs
/// </summary>
public static class FailingConnection
{
    public static DbConnection Create()
    {
        var connection = new pengdows.crud.fakeDb.fakeDbConnection();
        connection.SetFailOnOpen();
        return connection;
    }
}

/// <summary>
///     EF's null semantics context over the fixture's options, not tracking, with relational null semantics when asked. Behaviour
///     reference (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/Query/NullSemanticsQuerySqliteTest.cs
/// </summary>
public static class NullSemantics
{
    public static Microsoft.EntityFrameworkCore.TestModels.NullSemanticsModel.NullSemanticsContext CreateContext(
        Microsoft.EntityFrameworkCore.Query.NullSemanticsQueryFixtureBase fixture,
        bool useRelationalNulls)
    {
        var options = new DbContextOptionsBuilder(fixture.CreateOptions());
        if (useRelationalNulls)
        {
            new AdoNetDbContextOptionsBuilder(options).UseRelationalNulls();
        }

        var context = new Microsoft.EntityFrameworkCore.TestModels.NullSemanticsModel.NullSemanticsContext(options.Options);
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return context;
    }
}

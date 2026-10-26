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

/// <summary>A connection used by connection-interception tests to exercise an open failure.</summary>
public sealed class AdoNetThrowingConnection : DbConnection
{
    [global::System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get; set; } = "throwing";

    public override string Database => "throwing";

    public override string DataSource => "throwing";

    public override string ServerVersion => "0";

    public override System.Data.ConnectionState State => System.Data.ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName)
        => throw new NotSupportedException();

    public override void Close()
    {
    }

    public override void Open()
        => throw new InvalidOperationException("AdoNet connection open failure.");

    protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
        => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand()
        => throw new NotSupportedException();
}

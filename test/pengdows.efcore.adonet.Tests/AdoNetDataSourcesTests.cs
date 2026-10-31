using System.Data.Common;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Measures the data-source cache under dynamically built connection strings, as its remarks document: one entry per
///     distinct (factory, connection string) pair, kept until eviction or disposal.
/// </summary>
public class AdoNetDataSourcesTests
{
    [Fact]
    public void Each_distinct_connection_string_is_retained_until_disposal()
    {
        var factory = new CountingFactory();
        var sources = new AdoNetDataSources();

        for (var i = 0; i < 500; i++)
        {
            sources.GetOrCreate(factory, $"Data Source=tenant{i}");
        }

        Assert.Equal(500, sources.Count);

        sources.GetOrCreate(factory, "Data Source=tenant0");
        Assert.Equal(500, sources.Count);

        sources.Dispose();

        Assert.Equal(0, sources.Count);
        Assert.Equal(500, factory.Created.Count);
        Assert.All(factory.Created, d => Assert.True(d.Disposed));
    }

    [Fact]
    public void Eviction_removes_one_entry_and_disposes_its_data_source()
    {
        var factory = new CountingFactory();
        using var sources = new AdoNetDataSources();
        sources.GetOrCreate(factory, "Data Source=kept");
        sources.GetOrCreate(factory, "Data Source=retired");

        Assert.True(sources.Evict(factory, "Data Source=retired"));

        Assert.Equal(1, sources.Count);
        Assert.Single(factory.Created, d => d.Disposed);
    }

    [Fact]
    public void A_data_source_is_not_shared_between_factories_for_the_same_string()
    {
        using var sources = new AdoNetDataSources();

        sources.GetOrCreate(new CountingFactory(), "Data Source=same");
        sources.GetOrCreate(new CountingFactory(), "Data Source=same");

        Assert.Equal(2, sources.Count);
    }

    private sealed class CountingFactory : DbProviderFactory
    {
        public List<CountingDataSource> Created { get; } = [];

        public override DbDataSource CreateDataSource(string connectionString)
        {
            var source = new CountingDataSource(connectionString);
            Created.Add(source);
            return source;
        }
    }

    private sealed class CountingDataSource(string connectionString) : DbDataSource
    {
        public bool Disposed { get; private set; }

        public override string ConnectionString { get; } = connectionString;

        protected override DbConnection CreateDbConnection()
            => throw new NotSupportedException("Only the cache is measured.");

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}

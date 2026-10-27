namespace pengdows.efcore.adonet.Specification.Tests.FlatFile;

[Collection(SingleWriter.FlatFile)]
public class ProxyGraphUpdatesChangeTrackingFlatFileTest(ProxyGraphUpdatesAdoNetTest.ChangeTracking<FlatFileHost>.ProxyFixture fixture)
    : ProxyGraphUpdatesAdoNetTest.ChangeTracking<FlatFileHost>(fixture);

using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests;

// A lazy load starts a navigation query while the tracking reader is still open, so it needs a second reader on the connection.
// Whether a host's connections can hold one is the dialect's capability; a host without it skips these cases by that capability,
// naming the database, rather than by a provider name.
public abstract partial class LazyLoadProxyAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    [Theory]
    [InlineData(EntityState.Added)]
    [InlineData(EntityState.Unchanged)]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public override void Lazy_load_one_to_one_reference_with_recursive_property(EntityState state)
    {
        CapabilitySkips.RequireMultipleActiveReaders<THost>();
        base.Lazy_load_one_to_one_reference_with_recursive_property(state);
    }

    [Fact]
    public override void Top_level_projection_track_entities_before_passing_to_client_method()
    {
        CapabilitySkips.RequireMultipleActiveReaders<THost>();
        base.Top_level_projection_track_entities_before_passing_to_client_method();
    }
}

public abstract partial class LazyLoadProxyCoreAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    [Theory]
    [InlineData(EntityState.Added)]
    [InlineData(EntityState.Unchanged)]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public override void Lazy_load_one_to_one_reference_with_recursive_property(EntityState state)
    {
        CapabilitySkips.RequireMultipleActiveReaders<THost>();
        base.Lazy_load_one_to_one_reference_with_recursive_property(state);
    }

    [Fact]
    public override void Top_level_projection_track_entities_before_passing_to_client_method()
    {
        CapabilitySkips.RequireMultipleActiveReaders<THost>();
        base.Top_level_projection_track_entities_before_passing_to_client_method();
    }
}

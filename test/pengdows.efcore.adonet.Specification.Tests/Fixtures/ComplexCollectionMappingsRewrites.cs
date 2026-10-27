using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class ComplexTypesTrackingCoreAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            ComplexCollectionMappings.MapCollectionsToJson(modelBuilder);
        }
    }
}

public abstract partial class PropertyValuesCoreAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            ComplexCollectionMappings.MapCollectionsToJson(modelBuilder);
        }
    }
}

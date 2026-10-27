// Behaviour reference (not copied): Pomelo's TPCInheritanceQueryMySqlFixture (MIT, Pomelo Foundation):
// https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.FunctionalTests/Query/TPCInheritanceQueryMySqlFixture.cs

using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.Specification.Tests;

// TPC tables number their keys independently, so database-generated keys collide across a hierarchy's tables unless one
// sequence numbers them all. Until the core gives TPC hierarchies a shared sequence (on the databases that have
// sequences), these fixtures seed explicit keys and generate none.

internal static class TpcKeys
{
    public static void Explicit(ModelBuilder modelBuilder)
    {
        var roots = modelBuilder.Model.GetEntityTypes()
            .Where(entity => entity.BaseType == null && entity.GetMappingStrategy() == RelationalAnnotationNames.TpcMappingStrategy);
        foreach (var entity in roots)
        {
            foreach (var property in entity.FindPrimaryKey()!.Properties)
            {
                property.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}

public abstract partial class TPCInheritanceQueryAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        public override bool UseGeneratedKeys
            => false;

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            TpcKeys.Explicit(modelBuilder);
        }
    }
}

public abstract partial class TPCInheritanceBulkUpdatesAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        public override bool UseGeneratedKeys
            => false;

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            TpcKeys.Explicit(modelBuilder);
        }
    }
}

public abstract partial class TPCFiltersInheritanceBulkUpdatesAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        public override bool UseGeneratedKeys
            => false;

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            TpcKeys.Explicit(modelBuilder);
        }
    }
}

public abstract partial class TPCFiltersInheritanceQueryAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        public override bool UseGeneratedKeys
            => false;

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            TpcKeys.Explicit(modelBuilder);
        }
    }
}

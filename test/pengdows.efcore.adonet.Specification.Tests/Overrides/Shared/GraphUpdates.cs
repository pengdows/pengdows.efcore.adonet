namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class GraphUpdatesAdoNetTest<THost>
{
    // Where the database can't generate a column of a key of several (ISqlDialect.GeneratesCompositeKeyValues), the owned
    // collections take keys of their own and the tests of the default (owner, Id) keys are skipped, as Microsoft's SQLite
    // provider test does. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/GraphUpdates/GraphUpdatesSqliteTestBase.cs
    private static bool GeneratesCompositeKeyValues
        => new THost().Dialect.Sql.GeneratesCompositeKeyValues;

    private static void RequireCompositeKeyGeneration()
    {
        if (!GeneratesCompositeKeyValues)
        {
            var name = new THost().Dialect.Name;
            throw new CapabilitySkipException($"[SKIP:{name}] {name} can't generate a column of a key of several (efcore#26708).");
        }
    }

    public override Task Update_principal_with_shadow_key_owned_collection_throws(bool async)
    {
        RequireCompositeKeyGeneration();
        return base.Update_principal_with_shadow_key_owned_collection_throws(async);
    }

    public override Task Delete_principal_with_shadow_key_owned_collection_throws(bool async)
    {
        RequireCompositeKeyGeneration();
        return base.Delete_principal_with_shadow_key_owned_collection_throws(async);
    }

    public override Task Clearing_shadow_key_owned_collection_throws(bool async, bool useUpdate, bool addNew)
    {
        RequireCompositeKeyGeneration();
        return base.Clearing_shadow_key_owned_collection_throws(async, useUpdate, addNew);
    }

    public override Task Update_principal_with_CLR_key_owned_collection(bool async)
    {
        RequireCompositeKeyGeneration();
        return base.Update_principal_with_CLR_key_owned_collection(async);
    }

    public override Task Delete_principal_with_CLR_key_owned_collection(bool async)
    {
        RequireCompositeKeyGeneration();
        return base.Delete_principal_with_CLR_key_owned_collection(async);
    }

    public override Task Clearing_CLR_key_owned_collection(bool async, bool useUpdate, bool addNew)
    {
        RequireCompositeKeyGeneration();
        return base.Clearing_CLR_key_owned_collection(async, useUpdate, addNew);
    }

    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<AccessState>(b =>
            {
                b.Property(e => e.AccessStateId).ValueGeneratedNever();
                b.HasData(new AccessState { AccessStateId = 1 });
            });
            modelBuilder.Entity<Cruiser>(b =>
            {
                b.Property(e => e.IdUserState).HasDefaultValue(1);
                b.HasOne(e => e.UserState).WithMany(e => e.Users).HasForeignKey(e => e.IdUserState);
            });
            modelBuilder.Entity<AccessStateWithSentinel>(b =>
            {
                b.Property(e => e.AccessStateWithSentinelId).ValueGeneratedNever();
                b.HasData(new AccessStateWithSentinel { AccessStateWithSentinelId = 1 });
            });
            modelBuilder.Entity<CruiserWithSentinel>(b =>
            {
                b.Property(e => e.IdUserState).HasDefaultValue(1).Metadata.Sentinel = 667;
                b.HasOne(e => e.UserState).WithMany(e => e.Users).HasForeignKey(e => e.IdUserState);
            });
            modelBuilder.Entity<SomethingOfCategoryA>().Property<int>("CategoryId").HasDefaultValue(1);
            modelBuilder.Entity<SomethingOfCategoryB>().Property(e => e.CategoryId).HasDefaultValue(2);
            modelBuilder.Entity<CompositeKeyWith<int>>().Property(e => e.PrimaryGroup).HasDefaultValue(1).HasSentinel(1);
            modelBuilder.Entity<CompositeKeyWith<bool>>().Property(e => e.PrimaryGroup).HasDefaultValue(true);
            modelBuilder.Entity<CompositeKeyWith<bool?>>().Property(e => e.PrimaryGroup).HasDefaultValue(true);

            if (!GeneratesCompositeKeyValues)
            {
                modelBuilder.Entity<OwnerRoot>(b =>
                {
                    b.OwnsMany(e => e.OptionalChildren, b =>
                    {
                        b.HasKey("Id");
                        b.OwnsMany(e => e.Children, b => b.HasKey("Id"));
                    });
                    b.OwnsMany(e => e.RequiredChildren, b =>
                    {
                        b.HasKey("Id");
                        b.OwnsMany(e => e.Children, b => b.HasKey("Id"));
                    });
                });
            }
        }
    }
}

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class FindAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        // Where the database can't generate a column of a key of several (ISqlDialect.GeneratesCompositeKeyValues), the owned
        // collections take a key of their own, as Microsoft's SQLite provider test configures them. Behaviour reference (not
        // copied): https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/FindSqliteTest.cs
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            if (new THost().Dialect.Sql.GeneratesCompositeKeyValues)
            {
                return;
            }

            modelBuilder.Entity<IntKey>(b =>
            {
                b.OwnsOne(
                    e => e.OwnedReference, b =>
                    {
                        b.OwnsOne(e => e.NestedOwned);
                        b.OwnsMany(e => e.NestedOwnedCollection).ToTable("NestedOwnedCollection").HasKey(e => e.Prop);
                    });

                b.OwnsMany(
                    e => e.OwnedCollection, b =>
                    {
                        b.ToTable("OwnedCollection").HasKey(e => e.Prop);
                        b.OwnsOne(e => e.NestedOwned);
                        b.OwnsMany(e => e.NestedOwnedCollection).ToTable("OwnedNestedOwnedCollection").HasKey(e => e.Prop);
                    });
            });
        }
    }
}

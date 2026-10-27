// Behaviour reference (not copied): EF Core's StoreGeneratedSqlServerTestBase (MIT, .NET Foundation):
// https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.SqlServer.FunctionalTests/StoreGeneratedSqlServerTestBase.cs

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     The server-side values EF's StoreGenerated tests read back: defaults on the "store-generated" properties and a
///     computed column, which each provider's fixture configures. Every call here is portable EF model building; SQL
///     Server's own entities (hi-lo keys, a long key stored as a decimal) are left out.
/// </summary>
public abstract partial class StoreGeneratedAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.Entity<Gumball>(b =>
            {
                b.Property(e => e.Identity).HasDefaultValue("Banana Joe");
                b.Property(e => e.IdentityReadOnlyBeforeSave).HasDefaultValue("Doughnut Sheriff");
                b.Property(e => e.IdentityReadOnlyAfterSave).HasDefaultValue("Anton");
                b.Property(e => e.AlwaysIdentity).HasDefaultValue("Banana Joe");
                b.Property(e => e.AlwaysIdentityReadOnlyBeforeSave).HasDefaultValue("Doughnut Sheriff");
                b.Property(e => e.AlwaysIdentityReadOnlyAfterSave).HasDefaultValue("Anton");
                b.Property(e => e.Computed).HasDefaultValue("Alan");
                b.Property(e => e.ComputedReadOnlyBeforeSave).HasDefaultValue("Carmen");
                b.Property(e => e.ComputedReadOnlyAfterSave).HasDefaultValue("Tina Rex");
                b.Property(e => e.AlwaysComputed).HasDefaultValue("Alan");
                b.Property(e => e.AlwaysComputedReadOnlyBeforeSave).HasDefaultValue("Carmen");
                b.Property(e => e.AlwaysComputedReadOnlyAfterSave).HasDefaultValue("Tina Rex");
            });

            modelBuilder.Entity<Anais>(b =>
            {
                foreach (var property in b.Metadata.GetDeclaredProperties()
                             .Where(p => p.Name.StartsWith("OnAdd", StringComparison.Ordinal) || p.Name.StartsWith("OnUpdate", StringComparison.Ordinal))
                             .ToList())
                {
                    b.Property(property.Name).HasDefaultValue("Rabbit");
                }
            });

            modelBuilder.Entity<WithBackingFields>(b =>
            {
                b.Property(e => e.NullableAsNonNullable).HasComputedColumnSql("1");
                b.Property(e => e.NonNullableAsNullable).HasComputedColumnSql("1");
            });

            modelBuilder.Entity<WithNoBackingFields>(b =>
            {
                b.Property(e => e.TrueDefault).HasDefaultValue(true);
                b.Property(e => e.NonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.FalseDefault).HasDefaultValue(false);
                b.Property(e => e.ZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<WithNullableBackingFields>(b =>
            {
                b.Property(e => e.NullableBackedBoolTrueDefault).HasDefaultValue(true);
                b.Property(e => e.NullableBackedIntNonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.NullableBackedBoolFalseDefault).HasDefaultValue(false);
                b.Property(e => e.NullableBackedIntZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<WithObjectBackingFields>(b =>
            {
                b.Property(e => e.NullableBackedBoolTrueDefault).HasDefaultValue(true);
                b.Property(e => e.NullableBackedIntNonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.NullableBackedBoolFalseDefault).HasDefaultValue(false);
                b.Property(e => e.NullableBackedIntZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<NonStoreGenDependent>().Property(e => e.HasTemp).HasDefaultValue(777);

            // One column of a composite key, numbered by the database.
            modelBuilder.Entity<CompositePrincipal>().Property(e => e.Id).UseIdentityColumn();

            base.OnModelCreating(modelBuilder, context);
        }
    }
}

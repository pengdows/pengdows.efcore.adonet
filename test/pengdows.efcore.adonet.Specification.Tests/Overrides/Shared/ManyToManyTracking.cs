using Microsoft.EntityFrameworkCore.TestModels.ManyToManyModel;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class ManyToManyTrackingAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<JoinOneSelfPayload>().Property(e => e.Payload).HasDefaultValueSql("(UTC_TIMESTAMP())");
            modelBuilder.Entity<UnidirectionalJoinOneSelfPayload>().Property(e => e.Payload).HasDefaultValueSql("(UTC_TIMESTAMP())");

            modelBuilder.SharedTypeEntity<Dictionary<string, object>>("JoinOneToThreePayloadFullShared")
                .IndexerProperty<string>("Payload").HasMaxLength(255).HasDefaultValue("Generated");
            modelBuilder.Entity<JoinOneToThreePayloadFull>().Property(e => e.Payload).HasMaxLength(255).HasDefaultValue("Generated");

            modelBuilder.SharedTypeEntity<Dictionary<string, object>>("UnidirectionalJoinOneToThreePayloadFullShared")
                .IndexerProperty<string>("Payload").HasMaxLength(255).HasDefaultValue("Generated");
            modelBuilder.Entity<UnidirectionalJoinOneToThreePayloadFull>().Property(e => e.Payload)
                .HasMaxLength(255).HasDefaultValue("Generated");
        }
    }
}

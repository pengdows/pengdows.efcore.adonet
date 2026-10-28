using Microsoft.EntityFrameworkCore.TestModels.ManyToManyModel;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class ManyToManyTrackingAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            // The current time, as each upstream provider's test writes it (SQLite and Npgsql: CURRENT_TIMESTAMP and
            // NOW() AT TIME ZONE 'UTC'): the dialect's UTC time where it has one, an expression default in parentheses,
            // otherwise ISO's CURRENT_TIMESTAMP, which a default clause takes as it is.
            var utcNow = new THost().Dialect.Sql.Functions.UtcNow;
            var payloadDefault = utcNow == null ? "CURRENT_TIMESTAMP" : "(" + utcNow + ")";
            modelBuilder.Entity<JoinOneSelfPayload>().Property(e => e.Payload).HasDefaultValueSql(payloadDefault);
            modelBuilder.Entity<UnidirectionalJoinOneSelfPayload>().Property(e => e.Payload).HasDefaultValueSql(payloadDefault);

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

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>Excludes conversions that require a database to retain a DateTimeOffset's original offset.</summary>
public abstract partial class ValueConvertersEndToEndAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            if (!new THost().KeepsOffsets)
            {
                modelBuilder.Entity<ConvertingEntity>(entity =>
                {
                    entity.Ignore(value => value.StringToDateTimeOffset);
                    entity.Ignore(value => value.StringToNullableDateTimeOffset);
                    entity.Ignore(value => value.NullableStringToDateTimeOffset);
                    entity.Ignore(value => value.NullableStringToNullableDateTimeOffset);
                });
            }
        }
    }
}

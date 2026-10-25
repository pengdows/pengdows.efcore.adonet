using pengdows.efcore.adonet.Infrastructure;

namespace Microsoft.EntityFrameworkCore.TestUtilities;

public static class NpgsqlDbContextOptionsBuilderExtensions
{
    // pengdows: configures the AdoNet options builder in place of NpgsqlDbContextOptionsBuilder.
    public static AdoNetDbContextOptionsBuilder ApplyConfiguration(this AdoNetDbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);

        optionsBuilder.CommandTimeout(NpgsqlTestStore.CommandTimeout);

        return optionsBuilder;
    }
}

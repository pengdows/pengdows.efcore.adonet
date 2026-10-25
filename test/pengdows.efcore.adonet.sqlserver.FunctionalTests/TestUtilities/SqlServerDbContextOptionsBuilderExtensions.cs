// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using pengdows.efcore.adonet.Infrastructure;

namespace Microsoft.EntityFrameworkCore.TestUtilities;

public static class SqlServerDbContextOptionsBuilderExtensions
{
    // pengdows: for the AdoNet options builder; no retrying execution strategy until the plugin has one.
    public static AdoNetDbContextOptionsBuilder ApplyConfiguration(this AdoNetDbContextOptionsBuilder optionsBuilder)
    {
        var maxBatch = TestEnvironment.GetInt("MaxBatchSize");
        if (maxBatch.HasValue)
        {
            optionsBuilder.MaxBatchSize(maxBatch.Value);
        }

        optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);

        optionsBuilder.CommandTimeout(SqlServerTestStore.CommandTimeout);

        return optionsBuilder;
    }
}

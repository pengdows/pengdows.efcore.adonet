// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Data.SqlClient;

namespace Microsoft.EntityFrameworkCore.TestUtilities;

public class SqlServerTestHelpers : RelationalTestHelpers
{
    protected SqlServerTestHelpers()
    {
    }

    public static SqlServerTestHelpers Instance { get; } = new();

    // pengdows: the AdoNet core with the SQL Server dialect.
    public override IServiceCollection AddProviderServices(IServiceCollection services)
        => services.AddEntityFrameworkAdoNet(SqlServerDialect.Instance);

    public override DbContextOptionsBuilder UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseAdoNetSqlServer(new SqlConnection("Database=DummyDatabase"));

    // pengdows: the provider's definitions are internal; they come from its services.
    public override LoggingDefinitions LoggingDefinitions { get; } = ProviderServices.LoggingDefinitions(SqlServerDialect.Instance);
}

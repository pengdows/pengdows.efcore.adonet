namespace Microsoft.EntityFrameworkCore.TestUtilities;

public class NpgsqlTestHelpers : RelationalTestHelpers
{
    protected NpgsqlTestHelpers() { }

    public static NpgsqlTestHelpers Instance { get; } = new();

    // pengdows: the AdoNet core with the PostgreSQL dialect.
    public override IServiceCollection AddProviderServices(IServiceCollection services)
        => services.AddEntityFrameworkAdoNet(PostgreSqlDialect.Instance);

    public override DbContextOptionsBuilder UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseAdoNetPostgreSql(new NpgsqlConnection("Host=localhost;Database=DummyDatabase"));

    public override LoggingDefinitions LoggingDefinitions { get; } = ProviderServices.LoggingDefinitions(PostgreSqlDialect.Instance);
}

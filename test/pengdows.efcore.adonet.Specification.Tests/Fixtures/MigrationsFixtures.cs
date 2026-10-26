using Microsoft.EntityFrameworkCore.Scaffolding;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.Specification.Tests;

// The suite reads back what each migration made through the provider's schema reader, which it resolves from the
// context's services, as each upstream provider's fixture registers it. Behaviour reference (not copied):
// https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/Migrations/MigrationsSqliteTest.cs
public abstract partial class MigrationsAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
            => base.AddServices(serviceCollection).AddScoped<IDatabaseModelFactory, AdoNetDatabaseModelFactory>();
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Microsoft's <c>IsSqlite</c>: whether the context's dialect is SQLite's.</summary>
public static class SqliteDatabaseFacadeExtensions
{
    public static bool IsSqlite(this DatabaseFacade database)
        => ((IInfrastructure<IServiceProvider>)database).Instance.GetService<IAdoNetDialect>() is pengdows.efcore.adonet.sqlite.SqliteDialect;
}

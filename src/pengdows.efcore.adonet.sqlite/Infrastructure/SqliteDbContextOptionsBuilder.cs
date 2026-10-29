using pengdows.efcore.adonet.Infrastructure;

namespace Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
///     The options Microsoft's <c>UseSqlite</c> passes its configuration action: the provider's options for SQLite, the same
///     as <see cref="AdoNetDbContextOptionsBuilder" /> (Microsoft's public surface, implemented over this provider).
/// </summary>
public class SqliteDbContextOptionsBuilder : AdoNetDbContextOptionsBuilder
{
    public SqliteDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
        : base(optionsBuilder)
    {
    }

    internal SqliteDbContextOptionsBuilder(AdoNetDbContextOptionsBuilder options)
        : this(((IRelationalDbContextOptionsBuilderInfrastructure)options).OptionsBuilder)
    {
    }
}

using pengdows.efcore.adonet.Infrastructure;

namespace Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

/// <summary>
///     The options Npgsql's <c>UseNpgsql</c> passes its configuration action: the provider's options for PostgreSQL, the same
///     as <see cref="AdoNetDbContextOptionsBuilder" /> (Npgsql's public surface, implemented over this provider).
/// </summary>
public class NpgsqlDbContextOptionsBuilder : AdoNetDbContextOptionsBuilder
{
    public NpgsqlDbContextOptionsBuilder(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder optionsBuilder)
        : base(optionsBuilder)
    {
    }

    internal NpgsqlDbContextOptionsBuilder(AdoNetDbContextOptionsBuilder options)
        : this(((Microsoft.EntityFrameworkCore.Infrastructure.IRelationalDbContextOptionsBuilderInfrastructure)options).OptionsBuilder)
    {
    }
}

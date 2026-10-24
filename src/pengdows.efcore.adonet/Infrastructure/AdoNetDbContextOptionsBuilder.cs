using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace pengdows.efcore.adonet.Infrastructure;

public class AdoNetDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    : RelationalDbContextOptionsBuilder<AdoNetDbContextOptionsBuilder, AdoNetOptionsExtension>(optionsBuilder)
{
    public virtual AdoNetDbContextOptionsBuilder UseDialect(IAdoNetDialect dialect)
        => WithOption(e => e.WithDialect(dialect));
}

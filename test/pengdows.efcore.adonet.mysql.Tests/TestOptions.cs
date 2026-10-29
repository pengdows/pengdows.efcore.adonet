using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.mysql.Tests;

public static class TestOptions
{
    /// <summary>
    ///     Each distinct dialect and option set gets its own EF service provider, by design, and these tests use dozens (one per
    ///     server version, JSON package and Oracle-named option); EF treats more than twenty per process as an error unless
    ///     told otherwise, and fails whichever test happens to create the twenty-first.
    /// </summary>
    public static TBuilder AllowManyDialects<TBuilder>(this TBuilder builder)
        where TBuilder : DbContextOptionsBuilder
    {
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        return builder;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.Tests;

public static class TestOptions
{
    /// <summary>
    ///     Each distinct dialect gets its own EF service provider, by design, and these tests use dozens; EF treats more than
    ///     twenty per process as an error unless told otherwise.
    /// </summary>
    public static TBuilder AllowManyDialects<TBuilder>(this TBuilder builder)
        where TBuilder : DbContextOptionsBuilder
    {
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        return builder;
    }
}

using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Infrastructure;

/// <summary>Applies the common provider registration contract before provider-specific options.</summary>
internal static class AdoNetProviderOptions
{
    public static Action<AdoNetDbContextOptionsBuilder> Configure(
        IAdoNetDialect dialect,
        Action<AdoNetDbContextOptionsBuilder>? additionalOptions,
        ParameterTranslationMode? collectionMode = null)
        => options =>
        {
            options.UseDialect(dialect);
            if (collectionMode is { } mode)
            {
                options.UseParameterizedCollectionMode(mode);
            }

            additionalOptions?.Invoke(options);
        };
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>Marks single-column integer keys that the database generates (see <see cref="AdoNetAnnotationNames.Identity" />).</summary>
internal class AdoNetIdentityConvention : IModelFinalizingConvention
{
    public virtual void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.BaseType == null
                && entityType.GetTableName() != null
                && entityType.FindPrimaryKey() is { Properties: [var property] }
                && IsIdentity(property))
            {
                property.Builder.HasAnnotation(AdoNetAnnotationNames.Identity, true);
            }
        }
    }

    // The type the database stores, after any value converter: an integer key stored as text or bytes can't be numbered.
    // Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerValueGenerationStrategyConvention.cs
    private static bool IsIdentity(IConventionProperty property)
        => property.ValueGenerated == ValueGenerated.OnAdd
            && (property.GetValueConverter()?.ProviderClrType ?? property.GetProviderClrType() ?? property.ClrType) is var storedType
            && (Nullable.GetUnderlyingType(storedType) ?? storedType) is var unwrapped
            // An enum without a converter is stored as its underlying integer.
            && (unwrapped.IsEnum ? Enum.GetUnderlyingType(unwrapped) : unwrapped) is var clrType
            && (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short))
            && !property.IsForeignKey()
            && property.GetValueGeneratorFactory() == null
            // GetDefaultValue() is the CLR default (0) when none is configured.
            && !property.TryGetDefaultValue(out _)
            && property.GetDefaultValueSql() == null
            && property.GetComputedColumnSql() == null;
}

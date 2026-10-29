using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>Marks the integer key columns that the database generates (see <see cref="AdoNetAnnotationNames.Identity" />).</summary>
internal class AdoNetIdentityConvention : IModelFinalizingConvention
{
    public virtual void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            // Every key column EF generates on add: the key itself, or (an owned collection's) the number beside the owner's.
            if (entityType.BaseType == null
                && entityType.GetTableName() != null
                && entityType.FindPrimaryKey() is { } key)
            {
                foreach (var property in key.Properties.Where(IsIdentityKey))
                {
                    property.Builder.HasAnnotation(AdoNetAnnotationNames.Identity, true);
                }
            }
        }
    }

    // The type the database stores, after any value converter: an integer key stored as text or bytes can't be numbered.
    // Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerValueGenerationStrategyConvention.cs
    /// <summary>
    ///     Whether the database numbers this property: an integer key of its entity's table, generated on add. Shared by the
    ///     convention, which records it, and the migrations' annotation provider, which recognises it in a snapshot that no
    ///     convention ran for (as Pomelo derives it from the property, with no annotation at all).
    /// </summary>
    internal static bool IsIdentityKey(IReadOnlyProperty property)
        => property.DeclaringType is IReadOnlyEntityType { BaseType: null } entityType
            && entityType.GetTableName() != null
            && entityType.FindPrimaryKey() is { } key
            && key.Properties.Contains(property)
            && IsIdentity(property);

    private static bool IsIdentity(IReadOnlyProperty property)
        => property.ValueGenerated == ValueGenerated.OnAdd
            && (property.GetValueConverter()?.ProviderClrType ?? property.GetProviderClrType() ?? property.ClrType) is var storedType
            && (Nullable.GetUnderlyingType(storedType) ?? storedType) is var unwrapped
            // An enum without a converter is stored as its underlying integer.
            && (unwrapped.IsEnum ? Enum.GetUnderlyingType(unwrapped) : unwrapped) is var clrType
            && (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short))
            // A key that is the entity's own foreign key copies another row's. A TPT hierarchy's derived types tie their
            // keys to the root's through it too, but those foreign keys are theirs, not the root's.
            && !property.GetContainingForeignKeys().Any(fk => fk.DeclaringEntityType == property.DeclaringType)
            // EF's own conventions attach a temporary-value factory to database-generated keys; one the model asked for
            // means the application numbers the key.
            && (property.GetValueGeneratorFactory() == null
                || (property is IConventionProperty convention && convention.GetValueGeneratorFactoryConfigurationSource() == ConfigurationSource.Convention))
            // GetDefaultValue() is the CLR default (0) when none is configured.
            && !property.TryGetDefaultValue(out _)
            && property.GetDefaultValueSql() == null
            && property.GetComputedColumnSql() == null;
}

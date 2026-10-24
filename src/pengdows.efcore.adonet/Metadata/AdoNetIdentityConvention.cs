using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>Marks single-column integer keys that the database generates (see <see cref="AdoNetAnnotationNames.Identity" />).</summary>
public class AdoNetIdentityConvention : IModelFinalizingConvention
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

    private static bool IsIdentity(IConventionProperty property)
        => property.ValueGenerated == ValueGenerated.OnAdd
            && (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) is var clrType
            && (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short))
            && !property.IsForeignKey()
            && property.GetValueGeneratorFactory() == null
            // GetDefaultValue() is the CLR default (0) when none is configured.
            && !property.TryGetDefaultValue(out _)
            && property.GetDefaultValueSql() == null
            && property.GetComputedColumnSql() == null;
}

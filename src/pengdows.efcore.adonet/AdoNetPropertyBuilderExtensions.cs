using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace pengdows.efcore.adonet;

public static class AdoNetPropertyBuilderExtensions
{
    /// <summary>
    ///     Makes the column one the database numbers on insert (an identity, AUTO_INCREMENT, or the key's sequence, as the
    ///     dialect writes it). By convention only a single-column integer key is; this says so for any integer column, such as
    ///     one column of a composite key. The same name as EF's server providers' method.
    /// </summary>
    public static PropertyBuilder UseIdentityColumn(this PropertyBuilder propertyBuilder)
    {
        propertyBuilder.ValueGeneratedOnAdd();
        propertyBuilder.HasAnnotation(AdoNetAnnotationNames.Identity, true);
        return propertyBuilder;
    }

    /// <inheritdoc cref="UseIdentityColumn(PropertyBuilder)" />
    public static PropertyBuilder<TProperty> UseIdentityColumn<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => (PropertyBuilder<TProperty>)UseIdentityColumn((PropertyBuilder)propertyBuilder);
}

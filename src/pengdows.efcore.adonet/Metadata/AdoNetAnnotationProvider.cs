using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>Carries <see cref="AdoNetAnnotationNames.Identity" /> from properties to the columns migrations see.</summary>
internal class AdoNetAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : RelationalAnnotationProvider(dependencies)
{
    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        if (IsIdentity(column))
        {
            yield return new Annotation(AdoNetAnnotationNames.Identity, true);
        }
    }

    /// <summary>
    ///     Whether the database numbers this column: an identity property, in the table of the type that declares it. A table-
    ///     per-type derived table maps the same key property, but its key is the root row's, copied in; a table-per-concrete-
    ///     type table holds whole rows, so it numbers its own.
    /// </summary>
    internal static bool IsIdentity(IColumn column)
        => column.PropertyMappings.Any(m => HasIdentity(m.Property)
            && ((m.TableMapping.TypeBase == m.Property.DeclaringType)
                || (m.Property.DeclaringType.GetMappingStrategy() == RelationalAnnotationNames.TpcMappingStrategy)));

    // The convention's annotation when it ran. A model built without conventions (a migrations snapshot) has none, so the
    // convention's rule decides, as the database does: the same answer the convention would have recorded.
    private static bool HasIdentity(IReadOnlyProperty property)
        => property.FindAnnotation(AdoNetAnnotationNames.Identity) is { } annotation
            ? annotation.Value is true
            : AdoNetIdentityConvention.IsIdentityKey(property);
}

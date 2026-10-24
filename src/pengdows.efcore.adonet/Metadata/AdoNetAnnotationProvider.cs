using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>Carries <see cref="AdoNetAnnotationNames.Identity" /> from properties to the columns migrations see.</summary>
public class AdoNetAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : RelationalAnnotationProvider(dependencies)
{
    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        if (column.PropertyMappings.Any(m => m.Property.FindAnnotation(AdoNetAnnotationNames.Identity)?.Value is true))
        {
            yield return new Annotation(AdoNetAnnotationNames.Identity, true);
        }
    }
}

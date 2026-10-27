using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.Specification.Tests;

internal static class ComplexCollectionMappings
{
    public static void MapCollectionsToJson(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            MapCollectionsToJson(entityType, withinJsonContainer: false);
        }
    }

    private static void MapCollectionsToJson(IReadOnlyTypeBase typeBase, bool withinJsonContainer)
    {
        foreach (var property in typeBase.GetComplexProperties())
        {
            if (property.IsCollection && !withinJsonContainer)
            {
                ((IMutableComplexType)property.ComplexType).SetContainerColumnName(property.Name);
            }

            MapCollectionsToJson(property.ComplexType, withinJsonContainer || property.IsCollection);
        }
    }
}

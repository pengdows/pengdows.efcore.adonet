// Behaviour reference (not copied): EF Core's JSON SQLite fixtures (MIT, .NET Foundation):
// https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteFixture.cs

namespace pengdows.efcore.adonet.Specification.Tests;

// EF's JSON test models include collections of collections (bool[][] and the like), which relational providers don't map
// ("nested primitive collections are not yet supported"): leave them out of this provider's model until the shared EF
// mapping supports them.

internal static class NestedCollections
{
    public static void Ignore(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetDeclaredProperties().Where(IsNestedCollection).ToList())
            {
                entityType.RemoveProperty(property);
                entityType.AddIgnored(property.Name);
            }
        }
    }

    private static bool IsNestedCollection(IMutableProperty property)
        => property.Name.EndsWith("CollectionCollection", StringComparison.Ordinal);
}

public abstract partial class JsonQueryAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            NestedCollections.Ignore(modelBuilder);
        }
    }
}

public abstract partial class JsonUpdateAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            NestedCollections.Ignore(modelBuilder);
        }
    }
}

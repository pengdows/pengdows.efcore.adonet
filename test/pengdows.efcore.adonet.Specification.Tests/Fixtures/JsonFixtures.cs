// Portions adapted from EF Core's JsonQuerySqliteFixture and JsonUpdateSqliteFixture.
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace pengdows.efcore.adonet.Specification.Tests;

// EF's JSON test models include collections of collections (bool[][] and the like), which relational providers don't map
// ("nested primitive collections are not yet supported"): leave them out, as EF's own providers' fixtures do. Adapted from
// EF Core's JsonQuerySqliteFixture (MIT, .NET Foundation):
// https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteFixture.cs

internal static class NestedCollections
{
    public static void Ignore(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetDeclaredProperties().Where(p => p.Name.EndsWith("CollectionCollection", StringComparison.Ordinal)).ToList())
            {
                entityType.RemoveProperty(property);
                entityType.AddIgnored(property.Name);
            }
        }
    }
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

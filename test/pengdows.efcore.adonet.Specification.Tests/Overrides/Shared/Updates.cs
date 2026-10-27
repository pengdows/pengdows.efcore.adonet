using Microsoft.EntityFrameworkCore.TestModels.UpdatesModel;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
/// Restores the explicit hierarchy root used by EF's provider fixtures. The upstream fixture only calls Entity&lt;Product&gt;
/// and Entity&lt;ProductWithBytes&gt;, which is enough for providers whose conventions discover the abstract root while building
/// the model. Keeping the root explicit makes the shared ADO.NET fixture produce the canonical TPH ProductBase table.
/// </summary>
public abstract partial class UpdatesAdoNetTest<THost>
    where THost : IDatabaseHost, new()
{
    public partial class AdoNetFixture
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.Entity<ProductBase>();
            base.OnModelCreating(modelBuilder, context);
        }
    }
}

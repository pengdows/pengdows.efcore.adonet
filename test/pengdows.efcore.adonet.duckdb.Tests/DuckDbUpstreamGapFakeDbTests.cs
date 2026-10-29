using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>Failing parity probes for DuckDB native composite storage.</summary>
public sealed class DuckDbUpstreamGapFakeDbTests
{
    [Fact]
    public void Struct_properties_have_a_native_duckdb_struct_mapping()
    {
        var db = new FakeDuckDb();
        using var context = db.Context<StructContext>(options => new StructContext(options));
        var mappings = context.GetService<IRelationalTypeMappingSource>();

        Assert.NotNull(mappings.FindMapping(typeof(StructValue), "STRUCT(Color VARCHAR, Weight INTEGER)"));
    }

    private sealed class StructValue
    {
        public string Color { get; set; } = "";
        public int Weight { get; set; }
    }

    private sealed class StructRow
    {
        public int Id { get; set; }
        public StructValue Value { get; set; } = new();
    }

    private sealed class StructContext(DbContextOptions<StructContext> options) : DbContext(options)
    {
        public DbSet<StructRow> Rows => Set<StructRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StructRow>().Property(row => row.Value).HasColumnType("STRUCT(Color VARCHAR, Weight INTEGER)");
    }
}

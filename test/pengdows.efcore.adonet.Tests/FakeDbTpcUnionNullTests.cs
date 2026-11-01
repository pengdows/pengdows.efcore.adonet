using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A table-per-concrete-type query unions the concrete tables. A column a branch does not have is projected as NULL, and
///     that NULL must carry its column's type: PostgreSQL resolves two untyped NULLs in a UNION to text, which then cannot be
///     matched with a boolean in the next branch.
/// </summary>
public sealed class FakeDbTpcUnionNullTests
{
    [Fact]
    public void A_null_projected_by_a_union_branch_is_cast_to_its_column_type()
    {
        var database = new FakeDatabase();
        using var context = database.ContextOf<TpcContext>(AdoNetDialect.Ansi, options => new TpcContext(options));

        var sql = context.Animals.ToQueryString();

        Assert.Contains("UNION ALL", sql, StringComparison.Ordinal);
        Assert.Contains("CAST(NULL AS", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void A_null_outside_a_union_is_left_as_it_was()
    {
        var database = new FakeDatabase();
        using var context = database.ContextOf<PlainContext>(AdoNetDialect.Ansi, options => new PlainContext(options));

        var sql = context.Rows.Select(row => (bool?)null).ToQueryString();

        Assert.DoesNotContain("CAST(NULL AS", sql, StringComparison.Ordinal);
    }

    private abstract class Animal
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class Cat : Animal
    {
    }

    private sealed class Dog : Animal
    {
        public bool IsGood { get; set; }
    }

    private sealed class Fish : Animal
    {
        public int Fins { get; set; }
    }

    private sealed class PlainRow
    {
        public int Id { get; set; }
    }

    private sealed class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options)
    {
        public DbSet<PlainRow> Rows => Set<PlainRow>();
    }

    private sealed class TpcContext(DbContextOptions<TpcContext> options) : DbContext(options)
    {
        public DbSet<Animal> Animals => Set<Animal>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Animal>().UseTpcMappingStrategy();
            modelBuilder.Entity<Cat>();
            modelBuilder.Entity<Dog>();
            modelBuilder.Entity<Fish>();
        }
    }
}

using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Two tables that refer to each other form a cycle of foreign keys, which a database that can only declare a key in
///     CREATE TABLE cannot create: a model with one is refused when the context starts, naming the tables, where the dialect says
///     the database declares no such key (<see cref="ISqlDialect.SupportsForeignKeyCycles" />). A chain of references starts
///     everywhere, and a cycle starts where the database declares it.
/// </summary>
public class ForeignKeyCycleCapabilityTests
{
    [Fact]
    public void A_cycle_of_foreign_keys_is_refused_where_the_database_cannot_declare_one()
    {
        var database = new FakeDatabase();
        using var context = database.ContextOf<CycleContext>(TestDialects.NoForeignKeyCycles, o => new CycleContext(o, cycle: true));

        // Its own type, so a caller (the specification suite's capability skip) can tell the refusal from a model error.
        var error = Assert.Throws<pengdows.efcore.adonet.Infrastructure.ForeignKeyCyclesNotSupportedException>(() => context.Model);

        Assert.Contains("Alpha", error.Message);
        Assert.Contains("Beta", error.Message);
    }

    [Fact]
    public void A_chain_of_foreign_keys_starts_where_the_database_cannot_declare_a_cycle()
    {
        var database = new FakeDatabase();
        using var context = database.ContextOf<CycleContext>(TestDialects.NoForeignKeyCycles, o => new CycleContext(o, cycle: false));

        Assert.NotNull(context.Model.FindEntityType(typeof(Alpha)));
    }

    [Fact]
    public void A_cycle_of_foreign_keys_starts_where_the_database_declares_one()
    {
        var database = new FakeDatabase();
        using var context = database.ContextOf<CycleContext>(TestDialects.Ansi, o => new CycleContext(o, cycle: true));

        Assert.NotNull(context.Model.FindEntityType(typeof(Alpha)));
    }

    public sealed class Alpha
    {
        public int Id { get; set; }
        public int? BetaId { get; set; }
        public Beta? Beta { get; set; }
        public List<Beta> Betas { get; set; } = [];
    }

    public sealed class Beta
    {
        public int Id { get; set; }
        public int? AlphaId { get; set; }
        public Alpha? Alpha { get; set; }
        public List<Alpha> Alphas { get; set; } = [];
    }

    public sealed class CycleContext(DbContextOptions<CycleContext> options, bool cycle) : DbContext(options)
    {
        public DbSet<Alpha> Alphas => Set<Alpha>();
        public DbSet<Beta> Betas => Set<Beta>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (cycle)
            {
                // Alpha refers to Beta, and Beta refers back to Alpha: two keys, a cycle.
                modelBuilder.Entity<Alpha>().HasOne(a => a.Beta).WithMany(b => b.Alphas).HasForeignKey(a => a.BetaId);
                modelBuilder.Entity<Beta>().HasOne(b => b.Alpha).WithMany(a => a.Betas).HasForeignKey(b => b.AlphaId);
            }
            else
            {
                // Alpha refers to Beta, and Beta refers to nothing: one key, a chain.
                modelBuilder.Entity<Alpha>().HasOne(a => a.Beta).WithMany().HasForeignKey(a => a.BetaId);
                modelBuilder.Entity<Alpha>().Ignore(a => a.Betas);
                modelBuilder.Entity<Beta>().Ignore(b => b.Alpha).Ignore(b => b.AlphaId).Ignore(b => b.Alphas);
            }
        }
    }
}

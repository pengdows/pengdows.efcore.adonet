using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>Columns get the model's facets, and parameters bind with the column's, on a real server.</summary>
public class SqlServerFacetTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Named_decimals_and_ansi_strings_round_trip()
    {
        fixture.SkipIfNotRunnable();
        await fixture.CreateTablesOnceAsync<LedgerContext>(o => new(o));
        var big = 123_456_789_012_345_678_901m;

        await using (var context = fixture.CreateContext<LedgerContext>(o => new(o)))
        {
            context.Add(new Entry { Big = big, Code = "ascii" });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = fixture.CreateContext<LedgerContext>(o => new(o)))
        {
            Assert.Equal(1, await context.Entries.CountAsync(e => e.Big == big && e.Code == "ascii", Ct));
            Assert.Equal("varchar", await context.Database
                .SqlQuery<string>($"SELECT DATA_TYPE AS Value FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Entries' AND COLUMN_NAME = 'Code'")
                .SingleAsync(Ct));
        }
    }

    public class Entry
    {
        public int Id { get; set; }
        public decimal Big { get; set; }

        [MaxLength(50)]
        [Unicode(false)]
        public string Code { get; set; } = "";
    }

    public class LedgerContext(DbContextOptions<LedgerContext> options) : DbContext(options)
    {
        public DbSet<Entry> Entries => Set<Entry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Entry>().Property(e => e.Big).HasColumnType("decimal(38)");
    }
}

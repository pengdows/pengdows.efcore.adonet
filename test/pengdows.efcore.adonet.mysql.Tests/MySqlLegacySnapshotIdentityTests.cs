using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     A snapshot written by Pomelo (its integer key is ValueGeneratedOnAdd, with no identity annotation, which Pomelo derives
///     as AUTO_INCREMENT) diffed against the model: the key is already AUTO_INCREMENT in the database, so no ALTER adds one.
///     The snapshot is built without conventions, as EF's snapshots are. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Metadata/Internal/MySqlAnnotationProvider.cs
/// </summary>
public class MySqlLegacySnapshotIdentityTests
{
    public sealed class Family
    {
        public int Id { get; set; }
        public string LastName { get; set; } = "";
    }

    public sealed class FamilyContext(DbContextOptions<FamilyContext> options) : DbContext(options)
    {
        public DbSet<Family> Families => Set<Family>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Family>(b =>
            {
                b.Property(x => x.Id).ValueGeneratedOnAdd();
                b.ToTable("PeopleFamilies");
            });
        }
    }

    private sealed class PomeloSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Family>(b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("LastName");
                b.HasKey("Id");
                b.ToTable("PeopleFamilies");
            });
        }
    }

    [Fact]
    public void A_snapshot_integer_key_generated_on_add_is_not_altered_to_an_identity()
    {
        using var context = new FamilyContext(new DbContextOptionsBuilder<FamilyContext>().AllowManyDialects().UseAdoNetMySql("Server=localhost").Options);
        var source = context.GetService<IModelRuntimeInitializer>().Initialize(((IMutableModel)new PomeloSnapshot().Model).FinalizeModel());

        var operations = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(source.GetRelationalModel(), context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.DoesNotContain(
            operations.OfType<AlterColumnOperation>(),
            a => a.Name == "Id" && a.FindAnnotation("AdoNet:Identity")?.Value is true);
    }
}

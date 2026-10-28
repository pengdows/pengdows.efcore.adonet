using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>SQL Server's model conventions, checked on the model alone.</summary>
public class SqlServerConventionTests
{
    private static IModel Model<TContext>(Func<DbContextOptions<TContext>, TContext> create)
        where TContext : DbContext
    {
        using var context = create(new DbContextOptionsBuilder<TContext>().UseAdoNetSqlServer("Server=localhost").Options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    /// <summary>
    ///     A unique index over a nullable column skips its nulls, as SQL Server otherwise allows one: the filter names the
    ///     column delimited, whatever it is called. Behaviour reference (not copied):
    ///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerIndexConvention.cs
    /// </summary>
    [Fact]
    public void A_unique_index_over_a_nullable_column_filters_its_nulls_by_delimited_name()
    {
        var index = Model<OddColumnContext>(o => new OddColumnContext(o)).FindEntityType(typeof(OddColumn))!.GetIndexes().Single();

        Assert.Equal("[co]]de'x] IS NOT NULL", index.GetFilter());
    }

    /// <summary>A derived type's required column is nullable in its hierarchy's one table: the filter follows the column.</summary>
    [Fact]
    public void A_unique_index_over_a_derived_types_column_filters_the_nulls_of_other_types()
    {
        var index = Model<HierarchyContext>(o => new HierarchyContext(o)).FindEntityType(typeof(Derived))!.GetDeclaredIndexes().Single();

        Assert.Equal("[Number] IS NOT NULL", index.GetFilter());
    }

    private class Base
    {
        public int Id { get; set; }
    }

    private sealed class Derived : Base
    {
        public int Number { get; set; }
    }

    private sealed class HierarchyContext(DbContextOptions<HierarchyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Base>();
            modelBuilder.Entity<Derived>().HasIndex(d => d.Number).IsUnique();
        }
    }

    private sealed class OddColumn
    {
        public int Id { get; set; }
        public string? Code { get; set; }
    }

    private sealed class OddColumnContext(DbContextOptions<OddColumnContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OddColumn>(b =>
            {
                b.Property(o => o.Code).HasColumnName("co]de'x").HasMaxLength(20);
                b.HasIndex(o => o.Code).IsUnique();
            });
    }

    private static List<DeleteBehavior> DeleteBehaviors(IModel model)
        => model.GetEntityTypes().SelectMany(e => e.GetDeclaredForeignKeys()).Select(k => k.DeleteBehavior).ToList();

    [Fact]
    public void Of_two_cascade_paths_to_one_table_one_is_cascaded_by_ef()
    {
        var behaviors = DeleteBehaviors(Model<DiamondContext>(o => new DiamondContext(o, explicitRight: false)));

        Assert.Equal(3, behaviors.Count(b => b == DeleteBehavior.Cascade));
        Assert.Equal(1, behaviors.Count(b => b == DeleteBehavior.ClientCascade));
    }

    [Fact]
    public void An_explicit_cascade_is_kept_and_a_conventional_one_gives_way_to_it()
    {
        var model = Model<DiamondContext>(o => new DiamondContext(o, explicitRight: true));
        var leaf = model.FindEntityType(typeof(Leaf))!;

        Assert.Equal(DeleteBehavior.Cascade, leaf.GetForeignKeys().Single(k => k.PrincipalEntityType.ClrType == typeof(Right)).DeleteBehavior);
        Assert.Equal(1, DeleteBehaviors(model).Count(b => b == DeleteBehavior.ClientCascade));
    }

    [Fact]
    public void A_required_reference_to_its_own_table_is_cascaded_by_ef()
    {
        var node = Model<TreeContext>(o => new TreeContext(o)).FindEntityType(typeof(Node))!;

        Assert.Equal(DeleteBehavior.ClientCascade, node.GetForeignKeys().Single().DeleteBehavior);
    }

    /// <summary>Foreign keys of a hierarchy's types that share one column share one constraint, so one delete behaviour.</summary>
    [Fact]
    public void Foreign_keys_sharing_a_constraint_get_the_same_delete_behaviour()
    {
        var model = Model<SharedKeyContext>(o => new SharedKeyContext(o));
        var keys = model.GetEntityTypes().SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(k => k.DeclaringEntityType.ClrType.IsAssignableTo(typeof(Choice)) && k.PrincipalEntityType.ClrType == typeof(Quest))
            .ToList();

        Assert.Equal(2, keys.Count);
        Assert.Single(keys.Select(k => k.DeleteBehavior).Distinct());
    }

    private sealed class Quest
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public Root Root { get; set; } = null!;
    }

    private abstract class Choice
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public Root Root { get; set; } = null!;
    }

    private sealed class FirstChoice : Choice
    {
        public int QuestId { get; set; }
        public Quest Quest { get; set; } = null!;
    }

    private sealed class SecondChoice : Choice
    {
        public int QuestId { get; set; }
        public Quest Quest { get; set; } = null!;
    }

    // Root reaches Choice directly and through Quest: one of those paths must leave the database.
    private sealed class SharedKeyContext(DbContextOptions<SharedKeyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Choice>().ToTable("Choices");
            modelBuilder.Entity<FirstChoice>().Property(c => c.QuestId).HasColumnName("QuestId");
            modelBuilder.Entity<SecondChoice>().Property(c => c.QuestId).HasColumnName("QuestId");
            modelBuilder.Entity<Quest>();
        }
    }

    private sealed class Root
    {
        public int Id { get; set; }
    }

    private sealed class Left
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public Root Root { get; set; } = null!;
    }

    private sealed class Right
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public Root Root { get; set; } = null!;
    }

    private sealed class Leaf
    {
        public int Id { get; set; }
        public int LeftId { get; set; }
        public Left Left { get; set; } = null!;
        public int RightId { get; set; }
        public Right Right { get; set; } = null!;
    }

    private sealed class DiamondContext(DbContextOptions<DiamondContext> options, bool explicitRight) : DbContext(options)
    {
        public bool ExplicitRight
            => explicitRight;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var leaf = modelBuilder.Entity<Leaf>();
            if (explicitRight)
            {
                leaf.HasOne(l => l.Right).WithMany().OnDelete(DeleteBehavior.Cascade);
            }

            modelBuilder.Entity<Left>();
            modelBuilder.Entity<Right>();
        }

        // Each variant is a model of its own.
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, VariantKey>();
    }

    private sealed class VariantKey : Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), (context as DiamondContext)?.ExplicitRight, designTime);
    }

    private sealed class Node
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public Node Parent { get; set; } = null!;
    }

    private sealed class TreeContext(DbContextOptions<TreeContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Node>().HasOne(n => n.Parent).WithMany().HasForeignKey(n => n.ParentId).IsRequired();
    }
}

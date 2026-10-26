using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Only a key the database can number is an identity: one stored as an integer. An integer key stored as text or bytes
///     (through a value converter) can't be, on any database. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerValueGenerationStrategyConvention.cs
/// </summary>
public class IdentityConventionTests
{
    public class Numbered
    {
        public int Id { get; set; }
    }

    public class Coded
    {
        public int Id { get; set; }
    }

    public class Packed
    {
        public short Id { get; set; }
    }

    public enum Kind
    {
        First = 1
    }

    public class Kinded
    {
        public Kind Id { get; set; }
    }

    public class Animal
    {
        public int Id { get; set; }
    }

    public class Cat : Animal
    {
        public int Lives { get; set; }
    }

    public class Profile
    {
        public int Id { get; set; }
        public Animal Animal { get; set; } = null!;
    }

    public class Owner
    {
        public int Id { get; set; }
        public List<Line> Lines { get; set; } = [];
    }

    public class Line
    {
        public string Text { get; set; } = "";
    }

    public class Paired
    {
        public int Id { get; set; }
        public int Part { get; set; }
    }

    private sealed class KeysContext(DbContextOptions<KeysContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Numbered>();
            modelBuilder.Entity<Coded>().Property(e => e.Id).HasConversion<string>();
            modelBuilder.Entity<Packed>().Property(e => e.Id).HasConversion<byte[]>();
            modelBuilder.Entity<Kinded>().Property(e => e.Id).ValueGeneratedOnAdd();
            modelBuilder.Entity<Animal>().UseTptMappingStrategy();
            modelBuilder.Entity<Cat>();
            modelBuilder.Entity<Profile>().HasOne(p => p.Animal).WithOne().HasForeignKey<Profile>(p => p.Id);
            modelBuilder.Entity<Owner>().OwnsMany(o => o.Lines);
            modelBuilder.Entity<Paired>(b =>
            {
                b.HasKey(e => new { e.Id, e.Part });
                b.Property(e => e.Id).UseIdentityColumn();
            });
        }
    }

    private static bool IsIdentity<TEntity>()
    {
        using var context = new FakeDatabase().ContextOf<KeysContext>(AdoNetDialect.Ansi, o => new KeysContext(o));
        var property = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TEntity))!.FindProperty("Id")!;
        return property.FindAnnotation(AdoNetAnnotationNames.Identity)?.Value is true;
    }

    [Fact]
    public void An_integer_key_stored_as_an_integer_is_an_identity()
        => Assert.True(IsIdentity<Numbered>());

    [Fact]
    public void An_integer_key_stored_as_text_is_not()
        => Assert.False(IsIdentity<Coded>());

    [Fact]
    public void An_integer_key_stored_as_bytes_is_not()
        => Assert.False(IsIdentity<Packed>());

    [Fact]
    public void An_enum_key_stored_as_its_integer_is_an_identity()
        => Assert.True(IsIdentity<Kinded>());

    // EF ties a TPT derived table's key to the root's through the root's key property, which then reads as a foreign key:
    // the root's key is still the database's to number.
    [Fact]
    public void The_root_key_of_a_table_per_type_hierarchy_is_an_identity()
        => Assert.True(IsIdentity<Animal>());

    // The derived table's key is the root row's, copied in: only the root table numbers it.
    [Fact]
    public void Only_the_root_table_of_a_table_per_type_hierarchy_numbers_its_key()
    {
        using var context = new FakeDatabase().ContextOf<KeysContext>(AdoNetDialect.Ansi, o => new KeysContext(o));
        var script = context.Database.GenerateCreateScript();

        var root = script[script.IndexOf("CREATE TABLE \"Animal\"", StringComparison.Ordinal)..];
        var derived = script[script.IndexOf("CREATE TABLE \"Cat\"", StringComparison.Ordinal)..];
        Assert.Contains("IDENTITY", root[..root.IndexOf(");", StringComparison.Ordinal)]);
        Assert.DoesNotContain("IDENTITY", derived[..derived.IndexOf(");", StringComparison.Ordinal)]);
    }

    // A key that is also this entity's foreign key (a one-to-one sharing the principal's key) copies another row's key.
    [Fact]
    public void A_key_that_is_the_entitys_own_foreign_key_is_not()
        => Assert.False(IsIdentity<Profile>());

    // An owned collection's rows are keyed by their owner's key and a number of their own, which the database generates.
    [Fact]
    public void An_owned_collections_own_key_column_is_an_identity()
    {
        using var context = new FakeDatabase().ContextOf<KeysContext>(AdoNetDialect.Ansi, o => new KeysContext(o));
        var line = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Line))!;
        var key = line.FindPrimaryKey()!.Properties;

        Assert.Equal(2, key.Count);
        Assert.Equal([false, true], key.Select(p => p.FindAnnotation(AdoNetAnnotationNames.Identity)?.Value is true));
    }

    // A column of a composite key isn't numbered by convention; UseIdentityColumn says it is, as on EF's server providers.
    [Fact]
    public void A_column_of_a_composite_key_is_an_identity_when_asked()
        => Assert.True(IsIdentity<Paired>());
}

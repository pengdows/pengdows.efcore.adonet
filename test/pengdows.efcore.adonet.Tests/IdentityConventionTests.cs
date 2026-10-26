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

    // A column of a composite key isn't numbered by convention; UseIdentityColumn says it is, as on EF's server providers.
    [Fact]
    public void A_column_of_a_composite_key_is_an_identity_when_asked()
        => Assert.True(IsIdentity<Paired>());
}

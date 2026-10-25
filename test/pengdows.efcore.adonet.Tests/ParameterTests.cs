using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Parameters take their column's facets, so a query's parameters don't vary with the values bound to them.</summary>
public class ParameterTests
{
    private readonly FakeDatabase _db = new();

    private string ParametersOf(IAdoNetDialect dialect, Func<FacetContext, IQueryable<Coded>> query)
    {
        _db.Returns();
        using var context = _db.ContextOf<FacetContext>(dialect, o => new FacetContext(o));
        _ = query(context).ToList();
        return _db.Parameters.Last();
    }

    [Fact]
    public void A_string_parameter_takes_its_columns_size_when_the_value_fits()
    {
        var code = "ab";
        var longer = "abcdefghijkl";

        Assert.Equal("@code='ab' (Size = 10)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Code == code)));
        Assert.DoesNotContain("Size = 10", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Code == longer)));
    }

    [Fact]
    public void A_null_string_parameter_keeps_its_columns_size()
    {
        using var context = _db.ContextOf<FacetContext>(TestDialects.Ansi, o => new FacetContext(o));
        _db.Returns(FakeDatabase.Row("Id", 1));

        context.Add(new Coded { Code = null });
        context.SaveChanges();

        // The INSERT's columns name the parameters in order: find Code's.
        var columns = _db.Commands.Last().Split('(')[1].Split(')')[0].Split(", ").ToList();
        var code = $"@p{columns.IndexOf("\"Code\"")}=";
        Assert.Equal(code + "NULL (Size = 10)", _db.Parameters.Last().Split(", ").Single(p => p.StartsWith(code, StringComparison.Ordinal)));
    }

    [Fact]
    public void A_decimal_parameter_takes_its_columns_precision_and_scale()
    {
        var amount = 2.5m;

        Assert.Equal("@amount='2.5' (Precision = 10) (Scale = 4)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Amount > amount)));
        Assert.Equal("@amount='2.5' (Precision = 18) (Scale = 2)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Plain > amount)));
    }

    [Fact]
    public void Non_unicode_strings_bind_as_ansi()
    {
        var value = "ab";
        var tag = "abc";

        Assert.Equal("@value='ab' (DbType = AnsiString)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Ascii == value)));
        Assert.Equal("@tag='abc' (Size = 3) (DbType = AnsiStringFixedLength)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.AsciiTag == tag)));
    }

    [Fact]
    public void A_decimal_parameter_without_a_scale_takes_only_the_precision()
    {
        var amount = 2m;

        Assert.Equal("@amount='2' (Precision = 10)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Whole > amount)));
    }

    [Fact]
    public void A_decimal_with_an_explicit_store_type_takes_that_types_precision_and_scale()
    {
        var amount = 2.5m;

        Assert.Equal("@amount='2.5' (Precision = 10)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Typed > amount)));
        Assert.Equal("@amount='2.5' (Precision = 12) (Scale = 3)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.TypedWithScale > amount)));
        Assert.Equal("@amount='2.5'", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Untyped > amount)));
    }

    [Fact]
    public void A_precision_beyond_a_byte_is_left_to_the_driver()
    {
        var amount = 2.5m;

        Assert.Equal("@amount='2.5' (Scale = 2)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Huge > amount)));
    }

    [Fact]
    public void A_fixed_length_parameter_stays_fixed_only_when_the_value_fills_it()
    {
        var full = "abcde";
        var shorter = "ab";

        Assert.Equal("@full='abcde' (Size = 5) (DbType = StringFixedLength)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Tag == full)));
        Assert.Equal("@shorter='ab' (Size = 5)", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Tag == shorter)));
    }

    [Fact]
    public void An_unbounded_string_parameter_takes_the_dialects_size_when_the_value_fits()
    {
        var note = "x";
        var huge = new string('x', 5000);

        Assert.Equal("@note='x'", ParametersOf(TestDialects.Ansi, c => c.Coded.Where(x => x.Note == note)));
        Assert.Equal("@note='x' (Size = 4000)", ParametersOf(TestDialects.StringParameters4000, c => c.Coded.Where(x => x.Note == note)));
        // A column's own size wins over the dialect's.
        Assert.Equal("@note='x' (Size = 10)", ParametersOf(TestDialects.StringParameters4000, c => c.Coded.Where(x => x.Code == note)));
        Assert.DoesNotContain("Size = 4000", ParametersOf(TestDialects.StringParameters4000, c => c.Coded.Where(x => x.Note == huge)));
    }
}

public class FacetStoreTypeTests
{
    [Fact]
    public void Fixed_length_strings_are_char_columns()
    {
        using var context = new FakeDatabase().ContextOf<FacetContext>(TestDialects.Ansi, o => new FacetContext(o));
        var entity = context.Model.FindEntityType(typeof(Coded))!;

        Assert.Equal("CHAR(5)", entity.FindProperty(nameof(Coded.Tag))!.GetColumnType());
        Assert.Equal("VARCHAR(10)", entity.FindProperty(nameof(Coded.Code))!.GetColumnType());
    }

    [Fact]
    public void Fixed_length_binary_is_a_binary_column()
    {
        using var context = new FakeDatabase().ContextOf<FacetContext>(TestDialects.Ansi, o => new FacetContext(o));

        Assert.Equal("BINARY(32)", context.Model.FindEntityType(typeof(Coded))!.FindProperty(nameof(Coded.Hash))!.GetColumnType());
    }

    [Fact]
    public void The_dialect_is_told_whether_a_string_is_unicode()
    {
        using var context = new FakeDatabase().ContextOf<FacetContext>(TestDialects.NationalStrings, o => new FacetContext(o));
        var entity = context.Model.FindEntityType(typeof(Coded))!;

        Assert.Equal("NATIONAL CHARACTER VARYING(10)", entity.FindProperty(nameof(Coded.Code))!.GetColumnType());
        Assert.Equal("VARCHAR(10)", entity.FindProperty(nameof(Coded.AsciiCode))!.GetColumnType());
        Assert.Equal("NATIONAL CHARACTER VARYING", entity.FindProperty(nameof(Coded.Note))!.GetColumnType());
        Assert.Equal("VARCHAR", entity.FindProperty(nameof(Coded.Ascii))!.GetColumnType());
    }
}

/// <summary>Names unicode strings as ISO's national character type.</summary>
public sealed class NationalStringTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(string) ? "NATIONAL CHARACTER VARYING" : base.GetStoreType(clrType);

    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => clrType != typeof(string) ? base.GetStoreType(clrType, facets)
            : facets.Unicode ? (facets.Size is { } size ? $"NATIONAL CHARACTER VARYING({size})" : null)
            : facets.Size is { } ansiSize ? $"VARCHAR({ansiSize})" : "VARCHAR";
}

public class Coded
{
    public int Id { get; set; }

    [MaxLength(10)]
    public string? Code { get; set; }

    public string? Tag { get; set; }
    public string? Note { get; set; }

    [Precision(10, 4)]
    public decimal Amount { get; set; }

    public decimal Plain { get; set; }

    [Precision(10)]
    public decimal Whole { get; set; }

    [Unicode(false)]
    public string? Ascii { get; set; }

    [MaxLength(10)]
    [Unicode(false)]
    public string? AsciiCode { get; set; }

    public decimal Typed { get; set; }
    public decimal TypedWithScale { get; set; }
    public decimal Untyped { get; set; }

    [Precision(300, 2)]
    public decimal Huge { get; set; }

    public byte[]? Hash { get; set; }

    public string? AsciiTag { get; set; }
}

public class FacetContext(DbContextOptions<FacetContext> options) : DbContext(options)
{
    public DbSet<Coded> Coded => Set<Coded>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Coded>().Property(c => c.Tag).HasMaxLength(5).IsFixedLength();
        modelBuilder.Entity<Coded>().Property(c => c.AsciiTag).HasMaxLength(3).IsFixedLength().IsUnicode(false);
        modelBuilder.Entity<Coded>().Property(c => c.Typed).HasColumnType("DECIMAL(10)");
        modelBuilder.Entity<Coded>().Property(c => c.TypedWithScale).HasColumnType("NUMERIC(12,3)");
        modelBuilder.Entity<Coded>().Property(c => c.Untyped).HasColumnType("DECIMAL");
        modelBuilder.Entity<Coded>().Property(c => c.Hash).HasMaxLength(32).IsFixedLength();
    }
}

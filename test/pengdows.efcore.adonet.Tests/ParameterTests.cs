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

    public string? AsciiTag { get; set; }
}

public class FacetContext(DbContextOptions<FacetContext> options) : DbContext(options)
{
    public DbSet<Coded> Coded => Set<Coded>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Coded>().Property(c => c.Tag).HasMaxLength(5).IsFixedLength();
        modelBuilder.Entity<Coded>().Property(c => c.AsciiTag).HasMaxLength(3).IsFixedLength().IsUnicode(false);
    }
}

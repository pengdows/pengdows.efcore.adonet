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
}

public class FacetContext(DbContextOptions<FacetContext> options) : DbContext(options)
{
    public DbSet<Coded> Coded => Set<Coded>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Coded>().Property(c => c.Tag).HasMaxLength(5).IsFixedLength();
}

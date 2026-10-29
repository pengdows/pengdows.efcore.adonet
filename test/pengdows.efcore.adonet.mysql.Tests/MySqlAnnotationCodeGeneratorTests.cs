using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using pengdows.efcore.adonet.NetTopologySuite;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Scaffolded and snapshot code writes MySQL's annotations as the fluent methods that set them, as Pomelo's
///     MySqlAnnotationCodeGenerator does, not as HasAnnotation calls: every annotation the reader scaffolds or the model
///     builders set is consumed. Code generation only, no database. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Design/Internal/MySqlAnnotationCodeGenerator.cs
/// </summary>
public class MySqlAnnotationCodeGeneratorTests
{
    public sealed class Place
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Point Location { get; set; } = null!;
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class Tag
    {
        public string Code { get; set; } = "";
    }

    private sealed class PlaceContext(DbContextOptions<PlaceContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4", DelegationModes.ApplyToTables);
            modelBuilder.UseCollation("utf8mb4_bin", DelegationModes.ApplyToAll);
            modelBuilder.UseGuidCollation("ascii_bin");
            modelBuilder.Entity<Place>(b =>
            {
                b.HasCharSet("latin1");
                b.UseCollation("latin1_bin", DelegationModes.ApplyToTables);
                b.HasTableOption("ENGINE", "MyISAM");
                b.HasTableOption("ROW_FORMAT", "DYNAMIC");
                b.Property(p => p.Name).HasMaxLength(300).HasCharSet("ascii");
                b.Property(p => p.Location).HasSpatialReferenceSystem(4326);
                b.Property(p => p.UpdatedAt).UseMySqlComputedColumn();
                b.HasIndex(p => p.Name).IsFullText(true, "ngram").HasPrefixLength(10);
                b.HasIndex(p => p.Location).IsSpatial();
            });
            modelBuilder.Entity<Tag>(b =>
            {
                b.Property(t => t.Code).HasMaxLength(500);
                b.HasKey(t => t.Code).HasPrefixLength(20);
            });
        }
    }

    private static (IModel Model, IAnnotationCodeGenerator Generator) Design()
    {
        using var context = new PlaceContext(new DbContextOptionsBuilder<PlaceContext>().AllowManyDialects()
            .UseAdoNetMySql("Server=localhost", o => o.UseNetTopologySuite())
            .Options);

        // As EF's tools build it: the provider's design-time services.
        var services = new ServiceCollection();
        new MySqlDesignTimeServices().ConfigureDesignTimeServices(services);
        var generator = services.BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();
        return (context.GetService<IDesignTimeModel>().Model, generator);
    }

    // As EF's code generators call it: the annotations left after the ignored ones, consumed by the calls generated.
    private static (List<MethodCallCodeFragment> Calls, Dictionary<string, IAnnotation> Left) Calls(
        IAnnotationCodeGenerator generator,
        IAnnotatable annotatable,
        Func<Dictionary<string, IAnnotation>, IReadOnlyList<MethodCallCodeFragment>> generate)
    {
        var annotations = generator.FilterIgnoredAnnotations(annotatable.GetAnnotations()).ToDictionary(a => a.Name, a => a);
        var calls = generate(annotations).ToList();
        return (calls, annotations);
    }

    private static MethodCallCodeFragment Call(IEnumerable<MethodCallCodeFragment> calls, string method)
        => calls.Single(c => c.Method == method);

    private static void NothingOfMySqlLeft(Dictionary<string, IAnnotation> left)
        => Assert.DoesNotContain(left.Keys, k => k.StartsWith("MySql:", StringComparison.Ordinal) || (k == "Relational:Collation"));

    [Fact]
    public void The_models_character_set_collation_and_guid_collation_are_fluent_calls()
    {
        var (model, generator) = Design();

        var (calls, left) = Calls(generator, model, a => generator.GenerateFluentApiCalls(model, a));

        Assert.Equal(new object?[] { "utf8mb4", DelegationModes.ApplyToTables }, Call(calls, "HasCharSet").Arguments);
        Assert.Equal(new object?[] { "utf8mb4_bin", DelegationModes.ApplyToAll }, Call(calls, "UseCollation").Arguments);
        Assert.Equal(new object?[] { "ascii_bin" }, Call(calls, "UseGuidCollation").Arguments);
        NothingOfMySqlLeft(left);
    }

    [Fact]
    public void An_entity_types_character_set_collation_and_table_options_are_fluent_calls()
    {
        var (model, generator) = Design();
        var place = model.FindEntityType(typeof(Place))!;

        var (calls, left) = Calls(generator, place, a => generator.GenerateFluentApiCalls(place, a));

        Assert.Equal(new object?[] { "latin1", null }, Call(calls, "HasCharSet").Arguments);
        Assert.Equal(new object?[] { "latin1_bin", DelegationModes.ApplyToTables }, Call(calls, "UseCollation").Arguments);
        Assert.Equal(
            [new object?[] { "ENGINE", "MyISAM" }, new object?[] { "ROW_FORMAT", "DYNAMIC" }],
            calls.Where(c => c.Method == "HasTableOption").Select(c => c.Arguments.ToArray()));
        NothingOfMySqlLeft(left);
    }

    [Fact]
    public void A_propertys_character_set_srid_and_computed_value_are_fluent_calls()
    {
        var (model, generator) = Design();
        var place = model.FindEntityType(typeof(Place))!;
        var all = new List<MethodCallCodeFragment>();

        foreach (var property in place.GetProperties())
        {
            var (calls, left) = Calls(generator, property, a => generator.GenerateFluentApiCalls(property, a));
            all.AddRange(calls);
            NothingOfMySqlLeft(left);
        }

        Assert.Equal(new object?[] { "ascii" }, Call(all, "HasCharSet").Arguments);
        Assert.Equal(new object?[] { 4326 }, Call(all, "HasSpatialReferenceSystem").Arguments);
        Assert.Empty(Call(all, "UseMySqlComputedColumn").Arguments);
    }

    [Fact]
    public void Index_and_key_kinds_parsers_and_prefixes_are_fluent_calls()
    {
        var (model, generator) = Design();
        var place = model.FindEntityType(typeof(Place))!;
        var key = model.FindEntityType(typeof(Tag))!.FindPrimaryKey()!;
        var indexCalls = new List<MethodCallCodeFragment>();

        foreach (var index in place.GetIndexes())
        {
            var (calls, left) = Calls(generator, index, a => generator.GenerateFluentApiCalls(index, a));
            indexCalls.AddRange(calls);
            NothingOfMySqlLeft(left);
        }

        var (keyCalls, keyLeft) = Calls(generator, key, a => generator.GenerateFluentApiCalls(key, a));

        Assert.Equal(new object?[] { true, "ngram" }, Call(indexCalls, "IsFullText").Arguments);
        Assert.Equal(new object?[] { new[] { 10 } }, Call(indexCalls, "HasPrefixLength").Arguments);
        Assert.Contains(indexCalls, c => c.Method == "IsSpatial");
        Assert.Equal(new object?[] { new[] { 20 } }, Call(keyCalls, "HasPrefixLength").Arguments);
        NothingOfMySqlLeft(keyLeft);
    }

    [Fact]
    public void The_design_time_services_use_it()
    {
        var services = new ServiceCollection();

        new MySqlDesignTimeServices().ConfigureDesignTimeServices(services);

        Assert.Equal(typeof(MySqlAnnotationCodeGenerator), services.Last(s => s.ServiceType == typeof(IAnnotationCodeGenerator)).ImplementationType);
    }
}

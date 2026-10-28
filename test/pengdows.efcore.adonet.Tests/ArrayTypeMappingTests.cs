using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A primitive collection stored as the database's own array or list type, whose mapping is an
///     <see cref="IArrayTypeMapping" />: queried as the table of its elements through the mapping's template, and its
///     Contains, Count and element at a position through the mapping's functions where it names them. Behaviour reference
///     (not copied): Npgsql's array translation (PostgreSQL License):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Internal/NpgsqlQueryableMethodTranslatingExpressionVisitor.cs
/// </summary>
public class ArrayTypeMappingTests
{
    private static readonly IAdoNetDialect Arrays = new PartsDialect(
        "Arrays",
        services: s => new EntityFrameworkRelationalServicesBuilder(s).TryAdd<IRelationalTypeMappingSourcePlugin, ArrayPlugin>());

    private readonly FakeDatabase _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private BagContext Context()
        => _db.ContextOf<BagContext>(Arrays, o => new BagContext(o));

    [Fact]
    public void The_column_has_the_arrays_store_type()
    {
        using var context = Context();

        Assert.Contains("\"Numbers\" INTEGER ARRAY NOT NULL", context.Database.GenerateCreateScript());
    }

    [Fact]
    public async Task Contains_count_and_element_at_are_the_mappings_functions()
    {
        await using var context = Context();
        var three = 3;
        _db.Returns(FakeDatabase.Row("Id", 1));
        _db.Returns(new Dictionary<string, object?> { ["c"] = 2, ["e"] = 7 });

        var ids = await context.Bags.Where(b => b.Numbers.Contains(three)).Select(b => b.Id).ToListAsync(Ct);
        var row = await context.Bags.Select(b => new { Count = b.Numbers.Count, First = b.Numbers[0] }).SingleAsync(Ct);

        Assert.Equal([1], ids);
        Assert.Equal((2, 7), (row.Count, row.First));
        Assert.Contains("WHERE ARRAY_CONTAINS(\"b\".\"Numbers\", @three)", _db.Commands[0]);
        Assert.Contains("ARRAY_LENGTH(\"b\".\"Numbers\") AS \"Count\", \"b\".\"Numbers\"[0 + 1] AS \"First\"", _db.Commands[1]);
    }

    [Fact]
    public void Any_other_query_reads_the_table_of_its_elements()
    {
        using var context = Context();

        Assert.Matches(
            "FROM \\(SELECT \"value\", \"key\" FROM UNNEST\\(\"b\"\\.\"Numbers\"\\) WITH ORDINALITY AS u\\(\"value\", \"key\"\\)\\) AS \"\\w+\"",
            context.Bags.Select(b => b.Numbers.Where(n => n > 2).Sum()).ToQueryString());
    }

    public class Bag
    {
        public int Id { get; set; }
        public List<int> Numbers { get; set; } = [];
    }

    public sealed class BagContext(DbContextOptions<BagContext> options) : DbContext(options)
    {
        public DbSet<Bag> Bags => Set<Bag>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Bag>().Property(b => b.Numbers).HasColumnType("INTEGER ARRAY");
    }

    private sealed class ArrayPlugin : IRelationalTypeMappingSourcePlugin
    {
        public RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
            => (mappingInfo.ClrType == typeof(List<int>)) && (mappingInfo.StoreTypeName == "INTEGER ARRAY") ? new IntArrayMapping() : null;
    }

    private sealed class IntArrayMapping : RelationalTypeMapping, IArrayTypeMapping
    {
        private static readonly RelationalTypeMapping Element = new IntTypeMapping("INTEGER");

        public IntArrayMapping()
            : this(new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(List<int>),
                    comparer: new ListOfValueTypesComparer<List<int>, int>(Element.Comparer),
                    elementMapping: Element),
                "INTEGER ARRAY"))
        {
        }

        private IntArrayMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        public string Elements => "(SELECT {2}, {1} FROM UNNEST({0}) WITH ORDINALITY AS u({2}, {1}))";
        public string? Contains => "ARRAY_CONTAINS({0}, {1})";
        public string? Count => "ARRAY_LENGTH({0})";
        public string? ElementAt => "{0}[{1} + 1]";

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new IntArrayMapping(parameters);
    }
}

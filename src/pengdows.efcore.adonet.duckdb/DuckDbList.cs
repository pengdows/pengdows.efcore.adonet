using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.duckdb;

/// <summary>
///     Maps a <c>List&lt;T&gt;</c> column whose store type is a DuckDB LIST (<c>INTEGER[]</c>, <c>VARCHAR[]</c>...) to a
///     <see cref="DuckDbListTypeMapping{TElement}" />; DuckDB.NET binds and reads such a value as a <c>List&lt;T&gt;</c>. Any
///     other primitive collection stays JSON. Behaviour reference (not copied):
///     https://github.com/skuirrels/DuckDB.EFCoreProvider/tree/f49a8c3e25f28055ad139383d25e09cede39c6f8/src/DuckDB.EFCoreProvider/Storage
/// </summary>
internal sealed class DuckDbListTypeMappingSourcePlugin(IServiceProvider services) : IRelationalTypeMappingSourcePlugin
{
    public RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        if ((mappingInfo.ClrType is not { IsGenericType: true } clrType)
            || (clrType.GetGenericTypeDefinition() != typeof(List<>))
            || (mappingInfo.StoreTypeName is not { } storeType)
            || !storeType.EndsWith("[]", StringComparison.Ordinal))
        {
            return null;
        }

        // The type mapping source is built before any mapping is asked for; resolving it here, not in the constructor, avoids
        // the cycle of a plugin the source itself depends on.
        var source = services.GetRequiredService<IRelationalTypeMappingSource>();
        var elementType = clrType.GetGenericArguments()[0];
        var element = source.FindMapping(elementType, storeType[..^2]);
        return element == null
            ? null
            : (RelationalTypeMapping)Activator.CreateInstance(typeof(DuckDbListTypeMapping<>).MakeGenericType(elementType), element)!;
    }
}

/// <summary>A <c>List&lt;T&gt;</c> stored as a DuckDB LIST of the element's store type, queried with DuckDB's list functions.</summary>
internal sealed class DuckDbListTypeMapping<TElement> : RelationalTypeMapping, IArrayTypeMapping
{
    public DuckDbListTypeMapping(RelationalTypeMapping element)
        : this(new RelationalTypeMappingParameters(
            new CoreTypeMappingParameters(typeof(List<TElement>), comparer: ListComparer(element), elementMapping: element),
            element.StoreType + "[]"))
    {
    }

    private DuckDbListTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    // generate_subscripts numbers the elements from 1 beside unnest's rows, in the list's order.
    public string Elements => "(SELECT generate_subscripts({0}, 1) AS {1}, unnest({0}) AS {2})";
    public string? Contains => "list_contains({0}, {1})";
    public string? Count => "len({0})";
    public string? ElementAt => "{0}[{1} + 1]";

    private RelationalTypeMapping Element
        => (RelationalTypeMapping)ElementTypeMapping!;

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new DuckDbListTypeMapping<TElement>(parameters);

    // [a, b]::TYPE[]: the elements' own literals, the list's type stated so an empty list has one.
    protected override string GenerateNonNullSqlLiteral(object value)
        => "[" + string.Join(", ", ((IEnumerable<TElement>)value).Select(e => Element.GenerateSqlLiteral(e))) + "]::" + StoreType;

    private static ValueComparer ListComparer(RelationalTypeMapping element)
    {
        var comparerType = typeof(TElement).IsValueType
            ? Nullable.GetUnderlyingType(typeof(TElement)) is { } underlying
                ? typeof(ListOfNullableValueTypesComparer<,>).MakeGenericType(typeof(List<TElement>), underlying)
                : typeof(ListOfValueTypesComparer<,>).MakeGenericType(typeof(List<TElement>), typeof(TElement))
            : typeof(ListOfReferenceTypesComparer<,>).MakeGenericType(typeof(List<TElement>), typeof(TElement));
        return (ValueComparer)Activator.CreateInstance(comparerType, element.Comparer)!;
    }
}

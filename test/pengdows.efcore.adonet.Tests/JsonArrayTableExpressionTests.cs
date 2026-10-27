using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.Tests;

public sealed class JsonArrayTableExpressionTests
{
    [Fact]
    public void ElementType_reads_array_and_enumerable_element_types()
    {
        Assert.Equal(typeof(int), JsonArrayTableExpression.ElementType(typeof(int[])));
        Assert.Equal(typeof(string), JsonArrayTableExpression.ElementType(typeof(List<string>)));
    }

    [Fact]
    public void ElementType_rejects_non_enumerable_types()
        => Assert.Throws<ArgumentException>(() => JsonArrayTableExpression.ElementType(typeof(int)));
}

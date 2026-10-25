using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace Microsoft.EntityFrameworkCore.Query;

public class NorthwindWhereQueryNpgsqlTest : NorthwindWhereQueryRelationalTestBase<NorthwindQueryNpgsqlFixture<NoopModelCustomizer>>
{
    // ReSharper disable once UnusedParameter.Local
    public NorthwindWhereQueryNpgsqlTest(NorthwindQueryNpgsqlFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper)
        : base(fixture)
    {
        ClearLog();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public override async Task Where_compare_constructed_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_constructed_equal(async));

        AssertSql();
    }

    public override async Task Where_compare_constructed_multi_value_equal(bool async)
    {
        //  Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_equal(async));

        AssertSql();
    }

    public override async Task Where_compare_constructed_multi_value_not_equal(bool async)
    {
        //  Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_not_equal(async));

        AssertSql();
    }

    public override async Task Where_compare_tuple_constructed_equal(bool async)
    {
        await AssertQuery(
            async,
            ss => ss.Set<Customer>().Where(c => new Tuple<string>(c.City).Equals(new Tuple<string>("London"))));

        AssertSql(
            """
SELECT c."CustomerID", c."Address", c."City", c."CompanyName", c."ContactName", c."ContactTitle", c."Country", c."Fax", c."Phone", c."PostalCode", c."Region"
FROM "Customers" AS c
WHERE (c."City") = ('London')
""");
    }

    public override async Task Where_compare_tuple_constructed_multi_value_equal(bool async)
    {
        await AssertQuery(
            async,
            ss => ss.Set<Customer>().Where(
                c => new Tuple<string, string>(c.City, c.Country).Equals(new Tuple<string, string>("Sao Paulo", "Brazil"))));

        AssertSql(
            """
SELECT c."CustomerID", c."Address", c."City", c."CompanyName", c."ContactName", c."ContactTitle", c."Country", c."Fax", c."Phone", c."PostalCode", c."Region"
FROM "Customers" AS c
WHERE (c."City", c."Country") = ('Sao Paulo', 'Brazil')
""");
    }

    public override async Task Where_compare_tuple_constructed_multi_value_not_equal(bool async)
    {
        await AssertQuery(
            async,
            ss => ss.Set<Customer>().Where(
                c => !new Tuple<string, string>(c.City, c.Country).Equals(new Tuple<string, string>("Sao Paulo", "Brazil"))));

        AssertSql(
            """
SELECT c."CustomerID", c."Address", c."City", c."CompanyName", c."ContactName", c."ContactTitle", c."Country", c."Fax", c."Phone", c."PostalCode", c."Region"
FROM "Customers" AS c
WHERE c."City" <> 'Sao Paulo' OR c."City" IS NULL OR c."Country" <> 'Brazil' OR c."Country" IS NULL
""");
    }

    public override async Task Where_compare_tuple_create_constructed_equal(bool async)
    {
        //  Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_equal(async));

        AssertSql();
    }

    public override async Task Where_compare_tuple_create_constructed_multi_value_equal(bool async)
    {
        //  Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_equal(async));

        AssertSql();
    }

    public override async Task Where_compare_tuple_create_constructed_multi_value_not_equal(bool async)
    {
        //  Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_not_equal(async));

        AssertSql();
    }

    // pengdows: the "Row values" region is not copied. Its tests call the official provider's own EF.Functions row-value
    // comparisons (NpgsqlDbFunctionsExtensions) and resource strings, which this provider doesn't have yet.

    private void AssertSql(params string[] expected)
        => Fixture.TestSqlLoggerFactory.AssertBaseline(expected);

    protected override void ClearLog()
        => Fixture.TestSqlLoggerFactory.Clear();
}

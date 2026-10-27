namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     EF's Where suite. Where every upstream provider diverges from EF's base test the same way, the base suite does too
///     (citing them); where they disagree, the database's declared capability decides.
/// </summary>
public abstract class NorthwindWhereQueryAdoNetTest<THost>(NorthwindQueryAdoNetFixture<THost> fixture)
    : NorthwindWhereQueryRelationalTestBase<NorthwindQueryAdoNetFixture<THost>>(fixture)
    where THost : IDatabaseHost, new()
{
    private static bool RowValues
        => new THost().Dialect.Sql.RowValues;

    // Consensus: comparing anonymous types is untranslatable (EF issue #14672). Microsoft SQL Server and SQLite 10.0.11,
    // Npgsql 10.0.3 and skuirrels' DuckDB override these to expect the failure; Pomelo 9.0.1 inherits EF Core 9's base
    // tests, which expected it.
    public override Task Where_compare_constructed_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_equal(async));

    public override Task Where_compare_constructed_multi_value_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_equal(async));

    public override Task Where_compare_constructed_multi_value_not_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_not_equal(async));




    // Capability: tuples compare as row values where the database has them. Npgsql and DuckDB translate constructed tuples;
    // SQL Server and SQLite expect the failure. Tuple.Create comparisons fail on all five upstream providers; this provider
    // translates them too (beyond upstream).
    public override Task Where_compare_tuple_create_constructed_equal(bool async)
        => RowValues
            ? base.Where_compare_tuple_create_constructed_equal(async)
            : AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_equal(async));

    public override Task Where_compare_tuple_create_constructed_multi_value_equal(bool async)
        => RowValues
            ? base.Where_compare_tuple_create_constructed_multi_value_equal(async)
            : AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_equal(async));

    public override Task Where_compare_tuple_create_constructed_multi_value_not_equal(bool async)
        => RowValues
            ? base.Where_compare_tuple_create_constructed_multi_value_not_equal(async)
            : AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_not_equal(async));

    public override Task Where_compare_tuple_constructed_equal(bool async)
        => RowValues
            ? base.Where_compare_tuple_constructed_equal(async)
            : AssertTranslationFailed(() => base.Where_compare_tuple_constructed_equal(async));

    public override Task Where_compare_tuple_constructed_multi_value_equal(bool async)
        => RowValues
            ? base.Where_compare_tuple_constructed_multi_value_equal(async)
            : AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_equal(async));

    public override Task Where_compare_tuple_constructed_multi_value_not_equal(bool async)
        => RowValues
            ? base.Where_compare_tuple_constructed_multi_value_not_equal(async)
            : AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_not_equal(async));
}

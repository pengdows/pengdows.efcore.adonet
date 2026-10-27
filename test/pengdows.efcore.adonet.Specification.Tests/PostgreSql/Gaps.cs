namespace pengdows.efcore.adonet.Specification.Tests.PostgreSql;

// PostgreSQL folds unquoted identifiers to lower case. This upstream raw-SQL probe names the
// mixed-case table without delimiters, so it is a fixture defect rather than a provider SQL gap.
public partial class JsonUpdatePostgreSqlTest
{
    private const string PostgreSqlJsonGap = "[SKIP:PostgreSQL] Matches Npgsql's PostgreSQL baseline: nested primitive collections are not supported by EF's relational JSON model.";
    private const string PostgreSqlNulGap = "[SKIP:PostgreSQL] PostgreSQL jsonb/text cannot represent the NUL character (\\u0000), so this upstream JSON fixture cannot be stored.";

    [ConditionalTheory(Skip = "[SKIP:PostgreSQL] The upstream raw-SQL probe uses JsonEntitiesBasic without PostgreSQL identifier delimiters; PostgreSQL correctly resolves it as jsonentitiesbasic.")]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public override Task Add_and_update_top_level_optional_owned_collection_to_JSON(bool? value)
        => base.Add_and_update_top_level_optional_owned_collection_to_JSON(value);

    [ConditionalFact(Skip = PostgreSqlNulGap)]
    public override Task Edit_single_property_collection_of_char()
        => base.Edit_single_property_collection_of_char();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_bool()
        => base.Edit_single_property_collection_of_collection_of_bool();

    [ConditionalFact(Skip = PostgreSqlNulGap)]
    public override Task Edit_single_property_collection_of_collection_of_char()
        => base.Edit_single_property_collection_of_collection_of_char();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_double()
        => base.Edit_single_property_collection_of_collection_of_double();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_int16()
        => base.Edit_single_property_collection_of_collection_of_int16();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_int32()
        => base.Edit_single_property_collection_of_collection_of_int32();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_int64()
        => base.Edit_single_property_collection_of_collection_of_int64();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_nullable_enum_set_to_null()
        => base.Edit_single_property_collection_of_collection_of_nullable_enum_set_to_null();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_nullable_enum_with_int_converter()
        => base.Edit_single_property_collection_of_collection_of_nullable_enum_with_int_converter();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_nullable_int32()
        => base.Edit_single_property_collection_of_collection_of_nullable_int32();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_nullable_int32_set_to_null()
        => base.Edit_single_property_collection_of_collection_of_nullable_int32_set_to_null();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_collection_of_single()
        => base.Edit_single_property_collection_of_collection_of_single();

    [ConditionalFact(Skip = PostgreSqlJsonGap)]
    public override Task Edit_single_property_collection_of_signed_byte()
        => base.Edit_single_property_collection_of_signed_byte();

    [ConditionalTheory(Skip = PostgreSqlNulGap)]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public override Task Add_and_update_nested_optional_primitive_collection(bool? value)
        => base.Add_and_update_nested_optional_primitive_collection(value);
}

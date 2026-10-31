namespace pengdows.efcore.adonet.Specification.Tests.PostgreSql;

// A stored procedure's result set can't be composed into a query. PostgreSQL's Northwind routines are functions, which SQL composes,
// so the engine accepts the composition the suite expects to be refused.
public partial class FromSqlSprocQueryPostgreSqlTest
{
    private const string PostgreSqlComposedFunctionGap = "[SKIP:PostgreSQL] PostgreSQL's Northwind routines are set-returning functions, which SQL composes; the suite expects a stored procedure's result, which can't be composed, to be refused.";

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_stored_procedure_composed(bool async)
        => base.From_sql_queryable_stored_procedure_composed(async);

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_stored_procedure_with_parameter_composed(bool async)
        => base.From_sql_queryable_stored_procedure_with_parameter_composed(async);

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_stored_procedure_re_projection(bool async)
        => base.From_sql_queryable_stored_procedure_re_projection(async);

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_stored_procedure_and_select(bool async)
        => base.From_sql_queryable_stored_procedure_and_select(async);

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_stored_procedure_projection(bool async)
        => base.From_sql_queryable_stored_procedure_projection(async);

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_with_multiple_stored_procedures(bool async)
        => base.From_sql_queryable_with_multiple_stored_procedures(async);

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_select_and_stored_procedure(bool async)
        => base.From_sql_queryable_select_and_stored_procedure(async);

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_stored_procedure_min(bool async)
        => base.From_sql_queryable_stored_procedure_min(async);

    [ConditionalTheory(Skip = PostgreSqlComposedFunctionGap)]
    [InlineData(true)]
    [InlineData(false)]
    public override Task From_sql_queryable_stored_procedure_take(bool async)
        => base.From_sql_queryable_stored_procedure_take(async);
}

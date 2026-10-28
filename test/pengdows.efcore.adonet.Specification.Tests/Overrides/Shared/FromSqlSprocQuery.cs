namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class FromSqlSprocQueryAdoNetTest<THost>
{
    // EF's test composes over a procedure call and checks EF's message about it, so where there is no call to compose over the
    // message is EF's wrapping of why: the procedures are asked for first (CapabilitySkips.NoProcedures).
    public override Task From_sql_queryable_select_and_stored_procedure(bool async)
    {
        _ = Sprocs.Of<THost>();
        return base.From_sql_queryable_select_and_stored_procedure(async);
    }
}

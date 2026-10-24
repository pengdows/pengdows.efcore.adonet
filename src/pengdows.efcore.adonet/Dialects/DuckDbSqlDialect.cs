namespace pengdows.efcore.adonet.Dialects;

public sealed class DuckDbSqlDialect : AdoNetSqlDialect
{
    public static readonly DuckDbSqlDialect Instance = new();

    public override string Name => "DuckDB";
    public override string ParameterMarker => "$";
    public override bool ParameterNameIncludesMarker => false;
    public override PagingStyle Paging => PagingStyle.LimitOffset;
    public override bool SupportsMultipleStatementsPerCommand => true;
    public override int DefaultMaxBatchSize => 128;

    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BLOB" : base.GetStoreType(clrType);
}

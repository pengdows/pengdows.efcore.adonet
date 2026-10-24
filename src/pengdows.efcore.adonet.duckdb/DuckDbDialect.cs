namespace pengdows.efcore.adonet.duckdb;

public sealed class DuckDbDialect : AdoNetDialect
{
    public static readonly DuckDbDialect Instance = new();

    public override string Name => "DuckDB";
    public override ISqlDialect Sql { get; } = new DuckDbSql();
    public override ITypeMappingDialect TypeMappings { get; } = new DuckDbTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 128);
}

public sealed class DuckDbSql : AnsiSqlDialect
{
    public override string ParameterMarker => "$";
    public override bool ParameterNameIncludesMarker => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset();
}

public sealed class DuckDbTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BLOB" : base.GetStoreType(clrType);
}

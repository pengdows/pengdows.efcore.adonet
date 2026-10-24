namespace pengdows.efcore.adonet.postgresql;

public sealed class PostgreSqlDialect : AdoNetDialect
{
    public static readonly PostgreSqlDialect Instance = new();

    public override string Name => "PostgreSQL";
    public override ISqlDialect Sql { get; } = new PostgreSqlSql();
    public override ITypeMappingDialect TypeMappings { get; } = new PostgreSqlTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 128);
}

public sealed class PostgreSqlSql : AnsiSqlDialect
{
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset();

    // INFORMATION_SCHEMA.TABLES also lists the system catalogs.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' "
            + "AND TABLE_SCHEMA NOT IN ('pg_catalog', 'information_schema')";
}

public sealed class PostgreSqlTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BYTEA" : base.GetStoreType(clrType);
}

namespace pengdows.efcore.adonet.Dialects;

/// <summary>PostgreSQL (e.g. through Npgsql), using the ADO.NET abstractions only: no Npgsql-specific types or translations.</summary>
public sealed class PostgreSqlDialect : AdoNetSqlDialect
{
    public static readonly PostgreSqlDialect Instance = new();

    public override string Name => "PostgreSQL";
    public override PagingStyle Paging => PagingStyle.LimitOffset;
    public override bool SupportsMultipleStatementsPerCommand => true;
    public override int DefaultMaxBatchSize => 128;

    // INFORMATION_SCHEMA.TABLES also lists the system catalogs.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' "
            + "AND TABLE_SCHEMA NOT IN ('pg_catalog', 'information_schema')";

    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BYTEA" : base.GetStoreType(clrType);
}

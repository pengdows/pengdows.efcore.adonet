namespace pengdows.efcore.adonet.Dialects;

public class SqliteSqlDialect : AdoNetSqlDialect
{
    public static readonly SqliteSqlDialect Instance = new();

    public override string Name => "SQLite";
    public override bool SupportsSchemas => false;
    public override PagingStyle Paging => PagingStyle.LimitOffset;
    public override string? NoLimitValue => "-1";
    public override bool SupportsMultipleStatementsPerCommand => true;
    public override int DefaultMaxBatchSize => 128;

    public override string HasTablesSql
        => "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"rootpage\" IS NOT NULL";

    public override string? GetStoreType(Type clrType)
        => clrType == typeof(string) ? "TEXT"
            : clrType == typeof(byte[]) ? "BLOB"
            : clrType == typeof(Guid) ? "BLOB"
            : base.GetStoreType(clrType);
}

/// <summary>SQLite before 3.35 has no RETURNING; demonstrates the INSERT+SELECT path.</summary>
public sealed class LegacySqliteSqlDialect : SqliteSqlDialect
{
    public static new readonly LegacySqliteSqlDialect Instance = new();

    public override string Name => "SQLite (pre-3.35)";
    public override GeneratedValueRetrieval GeneratedValues => GeneratedValueRetrieval.SelectAfter;
    public override string? IdentityFunction => "last_insert_rowid()";
    public override string? RowsAffectedFunction => "changes()";
}

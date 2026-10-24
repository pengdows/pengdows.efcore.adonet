namespace pengdows.efcore.adonet.sqlite;

public class SqliteDialect : AdoNetDialect
{
    public static readonly SqliteDialect Instance = new();

    public override string Name => "SQLite";
    public override ISqlDialect Sql { get; } = new SqliteSql();
    public override ITypeMappingDialect TypeMappings { get; } = new SqliteTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 128);
}

/// <summary>SQLite before 3.35 has no RETURNING; keys come back through <c>last_insert_rowid()</c>.</summary>
public sealed class LegacySqliteDialect : SqliteDialect
{
    public static new readonly LegacySqliteDialect Instance = new();

    public override string Name => "SQLite (pre-3.35)";
    public override ISqlDialect Sql { get; } = new LegacySqliteSql();
}

public class SqliteSql : AnsiSqlDialect
{
    public override bool SupportsSchemas => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset(noLimitValue: "-1");

    public override string HasTablesSql
        => "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"rootpage\" IS NOT NULL";
}

public sealed class LegacySqliteSql : SqliteSql
{
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("last_insert_rowid()", "changes()");
}

public sealed class SqliteTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(string) ? "TEXT"
            : clrType == typeof(byte[]) || clrType == typeof(Guid) ? "BLOB"
            : base.GetStoreType(clrType);
}

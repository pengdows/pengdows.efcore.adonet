namespace pengdows.efcore.adonet.Dialects;

/// <summary>
///     MySQL (e.g. through MySqlConnector or MySql.Data). Backtick identifiers, a database is the schema, OFFSET needs a
///     LIMIT, and keys come back through <c>LAST_INSERT_ID()</c>. GUIDs are stored as <c>CHAR(36)</c> text.
/// </summary>
public sealed class MySqlSqlDialect : AdoNetSqlDialect
{
    public static readonly MySqlSqlDialect Instance = new();

    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "TINYINT(1)",
        [typeof(byte)] = "TINYINT UNSIGNED",
        [typeof(float)] = "FLOAT",
        [typeof(double)] = "DOUBLE",
        [typeof(string)] = "LONGTEXT",
        [typeof(DateTime)] = "DATETIME(6)",
        [typeof(TimeOnly)] = "TIME(6)",
        [typeof(TimeSpan)] = "TIME(6)",
        [typeof(Guid)] = "CHAR(36)",
        [typeof(byte[])] = "LONGBLOB"
    };

    public override string Name => "MySQL";
    public override string IdentifierQuotePrefix => "`";
    public override string IdentifierQuoteSuffix => "`";
    public override bool SupportsSchemas => false;
    public override PagingStyle Paging => PagingStyle.LimitOffset;
    public override string? NoLimitValue => "18446744073709551615";
    public override GeneratedValueRetrieval GeneratedValues => GeneratedValueRetrieval.SelectAfter;
    public override string? IdentityFunction => "LAST_INSERT_ID()";
    public override string? RowsAffectedFunction => "ROW_COUNT()";
    public override bool SupportsMultipleStatementsPerCommand => true;
    public override int DefaultMaxBatchSize => 128;

    // INFORMATION_SCHEMA.TABLES spans every database on the server.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = DATABASE()";

    // No time-zone-aware type.
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(DateTimeOffset) ? null : StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);
}

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     SQL Server. Identifiers use ANSI double quotes, which needs QUOTED_IDENTIFIER ON (the SqlClient default). BIT is not a
///     predicate, paging uses TOP or OFFSET/FETCH, and keys come from <c>SCOPE_IDENTITY()</c> so tables with triggers work.
/// </summary>
public sealed class SqlServerDialect : AdoNetDialect
{
    public static readonly SqlServerDialect Instance = new();

    public override string Name => "SQL Server";
    public override ISqlDialect Sql { get; } = new SqlServerSql();
    public override ITypeMappingDialect TypeMappings { get; } = new SqlServerTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 42);
}

public sealed class SqlServerSql : AnsiSqlDialect
{
    public override string TrueLiteral => "CAST(1 AS BIT)";
    public override string FalseLiteral => "CAST(0 AS BIT)";
    public override string LongCountFunction => "COUNT_BIG";
    public override PagingSyntax Paging => PagingSyntax.TopOrOffsetFetch;
    public override BooleanSyntax Booleans => BooleanSyntax.BitValues;
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("SCOPE_IDENTITY()", "@@ROWCOUNT");
    public override DmlSyntax Dml => DmlSyntax.TargetAliasFrom;
}

public sealed class SqlServerTypeMappings : AnsiTypeMappings
{
    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "BIT",
        [typeof(byte)] = "TINYINT",
        [typeof(double)] = "FLOAT",
        [typeof(string)] = "NVARCHAR(MAX)",
        [typeof(DateTime)] = "DATETIME2",
        [typeof(DateTimeOffset)] = "DATETIMEOFFSET",
        [typeof(TimeSpan)] = "TIME",
        [typeof(Guid)] = "UNIQUEIDENTIFIER",
        [typeof(byte[])] = "VARBINARY(MAX)"
    };

    public override string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);
}

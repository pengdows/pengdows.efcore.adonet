namespace pengdows.efcore.adonet.Dialects;

/// <summary>
///     SQL Server (e.g. through Microsoft.Data.SqlClient). Identifiers use ANSI double quotes, which needs
///     QUOTED_IDENTIFIER ON (the SqlClient default). BIT is not a predicate, paging uses TOP or OFFSET/FETCH,
///     and generated keys come from <c>SCOPE_IDENTITY()</c> so tables with triggers work.
/// </summary>
public sealed class SqlServerSqlDialect : AdoNetSqlDialect
{
    public static readonly SqlServerSqlDialect Instance = new();

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

    public override string Name => "SQL Server";
    public override PagingStyle Paging => PagingStyle.TopOrOffsetFetch;
    public override bool SupportsBooleanExpressions => false;
    public override string LongCountFunction => "COUNT_BIG";
    public override string TrueLiteral => "CAST(1 AS BIT)";
    public override string FalseLiteral => "CAST(0 AS BIT)";
    public override GeneratedValueRetrieval GeneratedValues => GeneratedValueRetrieval.SelectAfter;
    public override string? IdentityFunction => "SCOPE_IDENTITY()";
    public override string? RowsAffectedFunction => "@@ROWCOUNT";
    public override bool SupportsMultipleStatementsPerCommand => true;
    public override int DefaultMaxBatchSize => 42;

    public override string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);
}

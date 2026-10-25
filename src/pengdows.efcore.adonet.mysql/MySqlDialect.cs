using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL. Backtick identifiers, a database is the schema, OFFSET needs a LIMIT, and keys come back through
///     <c>LAST_INSERT_ID()</c>. GUIDs are stored as <c>CHAR(36)</c> text.
/// </summary>
public sealed class MySqlDialect : AdoNetDialect
{
    public static readonly MySqlDialect Instance = new();

    public override string Name => "MySQL";
    public override ISqlDialect Sql { get; } = new MySqlSql();
    public override ITypeMappingDialect TypeMappings { get; } = new MySqlTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 128, MaxParametersPerCommand: 65535);

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, MySqlMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, MySqlHistoryRepository>();
}

public sealed class MySqlSql : AnsiSqlDialect
{
    public override string IdentifierQuotePrefix => "`";
    public override string IdentifierQuoteSuffix => "`";
    public override bool SupportsSchemas => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset(noLimitValue: "18446744073709551615");
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("LAST_INSERT_ID()", "ROW_COUNT()");
    public override DmlSyntax Dml => DmlSyntax.MultiTable;
    public override FunctionSyntax Functions { get; } = new MySqlFunctions();

    // Backslash is an escape character in MySQL string literals (unless NO_BACKSLASH_ESCAPES is set).
    public override string StringLiteral(string value)
        => base.StringLiteral(value.Replace(@"\", @"\\"));

    // INFORMATION_SCHEMA.TABLES spans every database on the server.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = DATABASE()";
}

/// <summary><c>||</c> means OR. FLOOR and CEILING of a DECIMAL return BIGINT; adding 0.0 keeps the result a decimal.</summary>
public sealed class MySqlFunctions : FunctionSyntax
{
    public override string Concat => "CONCAT({0}, {1})";
    public override string? Floor => "(FLOOR({0}) + 0.0)";
    public override string? Ceiling => "(CEILING({0}) + 0.0)";
}

public sealed class MySqlTypeMappings : AnsiTypeMappings
{
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

    // No time-zone-aware type.
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(DateTimeOffset) ? null : StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);
}

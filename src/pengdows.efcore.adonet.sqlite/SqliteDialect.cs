using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.sqlite;

public class SqliteDialect : AdoNetDialect
{
    public static readonly SqliteDialect Instance = new();

    public override string Name => "SQLite";
    public override ISqlDialect Sql { get; } = new SqliteSql();
    public override ITypeMappingDialect TypeMappings { get; } = new SqliteTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 128, MaxParametersPerCommand: 32766);

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, SqliteMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, SqliteHistoryRepository>();
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
    public override FunctionSyntax Functions { get; } = new SqliteFunctions();

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

            // Every integer is INTEGER, which also makes any integer key the rowid.
            : clrType == typeof(long) || clrType == typeof(short) || clrType == typeof(byte) ? "INTEGER"
            : base.GetStoreType(clrType);
}

/// <summary>Dates are text, so their parts come from STRFTIME.</summary>
public sealed class SqliteFunctions : FunctionSyntax
{
    public override string? TrimStart => "LTRIM({0})";
    public override string? TrimEnd => "RTRIM({0})";
    public override string? Length => "LENGTH({0})";
    public override string? Substring => "SUBSTR({0}, {1}, {2})";
    public override string? SubstringFrom => "SUBSTR({0}, {1})";
    public override string? Position => "INSTR({1}, {0})";
    public override string? Year => "CAST(STRFTIME('%Y', {0}) AS INTEGER)";
    public override string? Month => "CAST(STRFTIME('%m', {0}) AS INTEGER)";
    public override string? Day => "CAST(STRFTIME('%d', {0}) AS INTEGER)";
    public override string? Hour => "CAST(STRFTIME('%H', {0}) AS INTEGER)";
    public override string? Minute => "CAST(STRFTIME('%M', {0}) AS INTEGER)";
    public override string? Second => "CAST(STRFTIME('%S', {0}) AS INTEGER)";
}

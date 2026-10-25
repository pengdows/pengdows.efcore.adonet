using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.postgresql;

public sealed class PostgreSqlDialect : AdoNetDialect
{
    public static readonly PostgreSqlDialect Instance = new();

    public override string Name => "PostgreSQL";
    public override ISqlDialect Sql { get; } = new PostgreSqlSql();
    public override ITypeMappingDialect TypeMappings { get; } = new PostgreSqlTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 128, MaxParametersPerCommand: 65535);

    public override void ConfigureServices(IServiceCollection services)
        => services.TryAddScoped<IAdoNetBulkLoader, PostgreSqlBulkLoader>();
}

public sealed class PostgreSqlSql : AnsiSqlDialect
{
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset();
    public override FunctionSyntax Functions { get; } = new PostgreSqlFunctions();

    // INFORMATION_SCHEMA.TABLES also lists the system catalogs.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' "
            + "AND TABLE_SCHEMA NOT IN ('pg_catalog', 'information_schema')";
}

public sealed class PostgreSqlTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BYTEA" : base.GetStoreType(clrType);

    // BYTEA takes no length.
    public override string? GetStoreType(Type clrType, int? size, int? precision, int? scale)
        => clrType == typeof(byte[]) ? null : base.GetStoreType(clrType, size, precision, scale);
}

/// <summary>ROUND with a number of places is only defined for numeric.</summary>
public sealed class PostgreSqlFunctions : FunctionSyntax
{
    public override string? RoundDigits => "ROUND(CAST({0} AS numeric), {1})";
}

using System.Globalization;
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
    public override string CurrentSchema => "CURRENT_SCHEMA";

    // INFORMATION_SCHEMA.TABLES also lists the system catalogs.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' "
            + "AND TABLE_SCHEMA NOT IN ('pg_catalog', 'information_schema')";
}

public sealed class PostgreSqlTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BYTEA" : base.GetStoreType(clrType);

    // X'..' is a bit string here; TIMESTAMP '...' would drop the offset; an interval in microseconds keeps sign and days.
    public override string FormatLiteral(object value, string storeType, string literal)
        => value switch
        {
            byte[] bytes => @"'\x" + Convert.ToHexString(bytes) + "'::bytea",
            DateTimeOffset offset => "TIMESTAMPTZ '" + offset.ToString(@"yyyy\-MM\-dd HH\:mm\:ss.FFFFFFzzz", CultureInfo.InvariantCulture) + "'",
            TimeSpan span => "INTERVAL '" + (span.Ticks / (TimeSpan.TicksPerMillisecond / 1000)).ToString(CultureInfo.InvariantCulture) + " microseconds'",
            _ => literal
        };

    // BYTEA takes no length.
    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => clrType == typeof(byte[]) ? null : base.GetStoreType(clrType, facets);
}

/// <summary>
///     ROUND with a number of places is only defined for numeric. A timestamptz holds an instant, whose parts EXTRACT takes in
///     the session's time zone; Npgsql reads it back at UTC, so its parts are taken at UTC.
/// </summary>
public sealed class PostgreSqlFunctions : FunctionSyntax
{
    public override string? OffsetDateTime => "({0} AT TIME ZONE 'UTC')";
    public override string? RoundDigits => "ROUND(CAST({0} AS numeric), {1})";
}

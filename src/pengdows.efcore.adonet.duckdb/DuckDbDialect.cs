using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.duckdb;

public sealed class DuckDbDialect : AdoNetDialect
{
    public static readonly DuckDbDialect Instance = new();

    public override string Name => "DuckDB";
    public override ISqlDialect Sql { get; } = new DuckDbSql();
    public override ITypeMappingDialect TypeMappings { get; } = new DuckDbTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 128, MaxParametersPerCommand: 65535);

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, DuckDbMigrationsSqlGenerator>();
}

public sealed class DuckDbSql : AnsiSqlDialect
{
    public override string ParameterMarker => "$";
    public override bool ParameterNameIncludesMarker => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset();
    public override FunctionSyntax Functions { get; } = new DuckDbFunctions();
}

/// <summary>
///     A TIMESTAMPTZ holds an instant, whose parts EXTRACT takes in the session's time zone; DuckDB.NET reads it back at UTC,
///     so its parts are taken at UTC.
/// </summary>
public sealed class DuckDbFunctions : FunctionSyntax
{
    public override string? OffsetDateTime => "({0} AT TIME ZONE 'UTC')";
}

public sealed class DuckDbTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BLOB" : base.GetStoreType(clrType);

    // A blob constant is a string of \xHH escapes cast to BLOB; TIMESTAMP '...' would drop an offset.
    public override string FormatLiteral(object value, string storeType, string literal)
        => value switch
        {
            byte[] bytes => "'" + string.Concat(bytes.Select(b => @"\x" + b.ToString("X2", CultureInfo.InvariantCulture))) + "'::BLOB",
            DateTimeOffset offset => "TIMESTAMPTZ '" + offset.ToString(@"yyyy\-MM\-dd HH\:mm\:ss.FFFFFFzzz", CultureInfo.InvariantCulture) + "'",
            _ => literal
        };

    // BLOB takes no length.
    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => clrType == typeof(byte[]) ? null : base.GetStoreType(clrType, facets);
}

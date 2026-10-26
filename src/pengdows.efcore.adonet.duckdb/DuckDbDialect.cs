using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.duckdb;

public sealed class DuckDbDialect : AdoNetDialect
{
    public static readonly DuckDbDialect Instance = new();

    private DuckDbDialect()
    {
    }

    public override string Name => "DuckDB";
    public override ISqlDialect Sql { get; } = new DuckDbSql();
    public override ITypeMappingDialect TypeMappings { get; } = new DuckDbTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
        MaxParametersPerCommand = 65535
    };

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, DuckDbMigrationsSqlGenerator>();
}

internal sealed class DuckDbSql : AnsiSqlDialect
{
    // A file with its .wal; :memory: (or no path) exists while open.
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.File(
        "Data Source",
        [".wal"],
        isInMemory: b => (Convert.ToString(b.TryGetValue("Data Source", out var path) ? path : null, CultureInfo.InvariantCulture) is not { Length: > 0 } text)
            || text.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase));

    public override string ParameterMarker => "$";
    public override bool ParameterNameIncludesMarker => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset();
    public override FunctionSyntax Functions { get; } = new DuckDbFunctions();
    public override string CurrentSchema => "current_schema()";

    // DuckDB has no savepoints.
    public override SavepointSyntax? Savepoints => null;
}

/// <summary>
///     A TIMESTAMPTZ holds an instant, whose parts EXTRACT takes in the session's time zone; DuckDB.NET reads it back at UTC,
///     so its parts are taken at UTC.
/// </summary>
internal sealed class DuckDbFunctions : FunctionSyntax
{
    // ^ is exponentiation in DuckDB; xor() is exclusive or. Behaviour reference (not copied):
    // https://github.com/skuirrels/DuckDB.EFCoreProvider/blob/f49a8c3e25f28055ad139383d25e09cede39c6f8/src/DuckDB.EFCoreProvider/Query/Internal/DuckDBSqlTranslatingExpressionVisitor.cs
    public override string? BitwiseAnd => "{0} & {1}";
    public override string? BitwiseOr => "{0} | {1}";
    public override string? BitwiseNot => "~{0}";
    public override string? ExclusiveOr => "xor({0}, {1})";

    // The % operator, as the database's EF Core provider writes it.
    public override string? Modulo => null;

    public override string? OffsetDateTime => "({0} AT TIME ZONE 'UTC')";
}

internal sealed class DuckDbTypeMappings : AnsiTypeMappings
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

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.flatfile;

/// <summary>
///     pengdows.flatfile: strict ISO SQL. Named parameters are <c>:name</c>, OFFSET/FETCH take literals only,
///     and there is no RETURNING or last-insert function, so keys come from <c>NEXT VALUE FOR</c> sequences.
/// </summary>
public sealed class FlatFileDialect : AdoNetDialect
{
    public static readonly FlatFileDialect Instance = new();

    private FlatFileDialect()
    {
    }

    public override string Name => "pengdows.flatfile";
    public override ISqlDialect Sql { get; } = new FlatFileSql();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 1
    };
    public override ITypeMappingDialect TypeMappings { get; } = new FlatFileTypeMappings();

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services).TryAdd<IMigrationsSqlGenerator, FlatFileMigrationsSqlGenerator>();
}

/// <summary>Binary data is a BLOB, of no declared length; there is no INTERVAL type, so no <see cref="TimeSpan" />.</summary>
internal sealed class FlatFileTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BLOB"
            : clrType == typeof(TimeSpan) ? null
            : base.GetStoreType(clrType);

    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => clrType == typeof(byte[]) ? null : base.GetStoreType(clrType, facets);
}

internal sealed class FlatFileSql : AnsiSqlDialect
{
    public override string ParameterMarker => ":";
    public override bool ParameterNameIncludesMarker => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.OffsetFetch.WithLiteralValues();
    public override KeyRetrieval Keys { get; } = KeyRetrieval.Sequence();
    public override DmlSyntax Dml => DmlSyntax.UnaliasedTarget;
    public override FunctionSyntax Functions { get; } = new FlatFileFunctions();
}

/// <summary>
///     There is no FLOOR; the SECOND field is already whole. LIKE ignores ESCAPE (an escaped pattern never matches), so
///     patterns that would need escaping are compared by position instead.
/// </summary>
internal sealed class FlatFileFunctions : FunctionSyntax
{
    public override char? LikeEscapeCharacter => null;
    public override string? Second => "EXTRACT(SECOND FROM {0})";
}

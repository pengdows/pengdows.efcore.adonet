namespace pengdows.efcore.adonet.flatfile;

/// <summary>
///     pengdows.flatfile: strict ISO SQL. Named parameters are <c>:name</c>, OFFSET/FETCH take literals only,
///     and there is no RETURNING or last-insert function, so keys come from <c>NEXT VALUE FOR</c> sequences.
/// </summary>
public sealed class FlatFileDialect : AdoNetDialect
{
    public static readonly FlatFileDialect Instance = new();

    public override string Name => "pengdows.flatfile";
    public override ISqlDialect Sql { get; } = new FlatFileSql();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 1);
    public override ITypeMappingDialect TypeMappings { get; } = new FlatFileTypeMappings();
}

/// <summary>Binary data is a BLOB, of no declared length; there is no INTERVAL type, so no <see cref="TimeSpan" />.</summary>
public sealed class FlatFileTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BLOB"
            : clrType == typeof(TimeSpan) ? null
            : base.GetStoreType(clrType);

    public override string? GetStoreType(Type clrType, int? size, int? precision, int? scale, bool fixedLength)
        => clrType == typeof(byte[]) ? null : base.GetStoreType(clrType, size, precision, scale, fixedLength);
}

public sealed class FlatFileSql : AnsiSqlDialect
{
    public override string ParameterMarker => ":";
    public override bool ParameterNameIncludesMarker => false;
    public override PagingSyntax Paging { get; } = PagingSyntax.OffsetFetch.WithLiteralValues();
    public override KeyRetrieval Keys { get; } = KeyRetrieval.Sequence();
    public override DmlSyntax Dml => DmlSyntax.UnaliasedTarget;
    public override FunctionSyntax Functions { get; } = new FlatFileFunctions();
}

/// <summary>There is no FLOOR; the SECOND field is already whole.</summary>
public sealed class FlatFileFunctions : FunctionSyntax
{
    public override string? Second => "EXTRACT(SECOND FROM {0})";
}

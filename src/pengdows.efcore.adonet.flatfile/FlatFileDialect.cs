namespace pengdows.efcore.adonet.flatfile;

/// <summary>
///     pengdows.flatfile: strict ISO SQL. Named parameters are <c>:name</c>; there is no RETURNING or last-insert function,
///     so keys come from <c>NEXT VALUE FOR</c> sequences; and there is no <c>UPDATE ... FROM</c>.
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
}

internal sealed class FlatFileSql : AnsiSqlDialect
{
    public override string ParameterMarker => ":";
    public override bool ParameterNameIncludesMarker => false;
    public override KeyRetrieval Keys { get; } = KeyRetrieval.Sequence();
    public override DmlSyntax Dml => DmlSyntax.UnaliasedTarget;
}

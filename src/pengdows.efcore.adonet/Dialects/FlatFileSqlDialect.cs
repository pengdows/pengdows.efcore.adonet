namespace pengdows.efcore.adonet.Dialects;

/// <summary>
///     pengdows.flatfile: strict ISO SQL. Named parameters are <c>:name</c>, OFFSET/FETCH take literals only,
///     and there is no RETURNING or last-insert function, so keys come from <c>NEXT VALUE FOR</c> sequences.
/// </summary>
public sealed class FlatFileSqlDialect : AdoNetSqlDialect
{
    public static readonly FlatFileSqlDialect Instance = new();

    public override string Name => "pengdows.flatfile";
    public override string ParameterMarker => ":";
    public override bool ParameterNameIncludesMarker => false;
    public override bool SupportsParametersInPaging => false;
    public override GeneratedValueRetrieval GeneratedValues => GeneratedValueRetrieval.RecordsAffected;
    public override bool SupportsMultipleStatementsPerCommand => true;

    // The ISO form; a bare SELECT without FROM is a vendor extension.
    public override string GetNextSequenceValueSql(string delimitedSequenceName)
        => "VALUES (NEXT VALUE FOR " + delimitedSequenceName + ")";
}

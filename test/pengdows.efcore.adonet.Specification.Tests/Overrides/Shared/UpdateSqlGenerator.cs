namespace pengdows.efcore.adonet.Specification.Tests;

// EF's expected SQL quotes names ISO's way by default; each dialect's own quotes (MySQL's backticks) are what it writes.
public abstract partial class UpdateSqlGeneratorAdoNetTest<THost>
{
    protected override string OpenDelimiter
        => new THost().Dialect.Sql.IdentifierQuotePrefix;

    protected override string CloseDelimiter
        => new THost().Dialect.Sql.IdentifierQuoteSuffix;
}

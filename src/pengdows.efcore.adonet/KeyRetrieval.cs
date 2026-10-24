namespace pengdows.efcore.adonet;

/// <summary>
///     How generated keys and concurrency checks come back from the database. One object spans the SQL shape, how results
///     are read and, for sequences, how keys are drawn before the INSERT, so those can't disagree. A database needing a
///     strategy not listed here registers its own <see cref="Microsoft.EntityFrameworkCore.Update.IUpdateSqlGenerator" /> in
///     <see cref="IAdoNetDialect.ConfigureServices" />.
/// </summary>
public abstract class KeyRetrieval
{
    private protected KeyRetrieval()
    {
    }

    /// <summary><c>INSERT ... RETURNING col</c> / <c>UPDATE ... RETURNING 1</c>.</summary>
    public static ReturningKeyRetrieval Returning { get; } = new();

    /// <summary>
    ///     <c>INSERT ...; SELECT col FROM t WHERE {rowsAffectedFunction} = 1 AND key = {identityFunction};</c>, e.g.
    ///     <c>SCOPE_IDENTITY()</c>/<c>@@ROWCOUNT</c>. Requires <see cref="IDatabaseCapabilities.SupportsMultipleStatementsPerCommand" />.
    /// </summary>
    public static SelectAfterKeyRetrieval SelectAfter(string identityFunction, string rowsAffectedFunction)
        => new(identityFunction, rowsAffectedFunction);

    /// <summary>
    ///     Plain statements, one per command, checked through <see cref="System.Data.Common.DbDataReader.RecordsAffected" />.
    ///     Nothing can be read back, so integer keys are drawn from a sequence before the INSERT, and other store-generated
    ///     values are unsupported. Defaults: sequence <c>"{table}_{column}_seq"</c>, read with <c>VALUES (NEXT VALUE FOR seq)</c>.
    /// </summary>
    public static SequenceKeyRetrieval Sequence(
        Func<string, string, string>? sequenceName = null,
        Func<string, string>? nextValueSql = null)
        => new(sequenceName ?? ((table, column) => $"{table}_{column}_seq"),
            nextValueSql ?? (sequence => "VALUES (NEXT VALUE FOR " + sequence + ")"));
}

public sealed class ReturningKeyRetrieval : KeyRetrieval;

public sealed class SelectAfterKeyRetrieval(string identityFunction, string rowsAffectedFunction) : KeyRetrieval
{
    public string IdentityFunction { get; } = identityFunction;
    public string RowsAffectedFunction { get; } = rowsAffectedFunction;
}

public sealed class SequenceKeyRetrieval(Func<string, string, string> sequenceName, Func<string, string> nextValueSql)
    : KeyRetrieval
{
    /// <summary>The sequence backing an integer key column.</summary>
    public string GetSequenceName(string table, string column)
        => sequenceName(table, column);

    /// <summary>SQL returning one row with the sequence's next value; the name is already delimited.</summary>
    public string GetNextValueSql(string delimitedSequenceName)
        => nextValueSql(delimitedSequenceName);
}

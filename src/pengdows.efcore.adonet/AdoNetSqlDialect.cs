using System.Data.Common;
using pengdows.efcore.adonet.Dialects;

namespace pengdows.efcore.adonet;

public enum PagingStyle
{
    /// <summary><c>OFFSET n ROWS FETCH NEXT m ROWS ONLY</c> (SQL:2008).</summary>
    OffsetFetch,

    /// <summary><c>LIMIT m OFFSET n</c>.</summary>
    LimitOffset
}

public enum GeneratedValueRetrieval
{
    /// <summary><c>INSERT ... RETURNING col</c> / <c>UPDATE ... RETURNING 1</c>.</summary>
    Returning,

    /// <summary>
    ///     <c>INSERT ...; SELECT col FROM t WHERE {RowsAffectedFunction} = 1 AND key = {IdentityFunction};</c>.
    ///     Requires <see cref="AdoNetSqlDialect.SupportsMultipleStatementsPerCommand" />.
    /// </summary>
    SelectAfter,

    /// <summary>
    ///     Plain statements, one per command; rows affected come from <see cref="DbDataReader.RecordsAffected" />.
    ///     Nothing can be read back, so integer keys are drawn from a sequence before the insert
    ///     (see <see cref="AdoNetSqlDialect.GetKeySequenceName" />) and other store-generated values are unsupported.
    /// </summary>
    RecordsAffected
}

/// <summary>
///     Everything the provider needs to know about a database's SQL that ADO.NET does not tell us.
///     Defaults are ANSI SQL. Dialects are cached per instance in EF's internal service provider,
///     so share instances (e.g. <see cref="Ansi" />) instead of creating one per DbContext.
/// </summary>
public class AdoNetSqlDialect
{
    public static readonly AdoNetSqlDialect Ansi = new();

    private static readonly Dictionary<Type, string> DefaultStoreTypes = new()
    {
        [typeof(bool)] = "BOOLEAN",
        [typeof(byte)] = "SMALLINT",
        [typeof(short)] = "SMALLINT",
        [typeof(int)] = "INTEGER",
        [typeof(long)] = "BIGINT",
        [typeof(float)] = "REAL",
        [typeof(double)] = "DOUBLE PRECISION",
        [typeof(decimal)] = "DECIMAL(18,2)",
        [typeof(string)] = "VARCHAR",
        [typeof(DateTime)] = "TIMESTAMP",
        [typeof(DateTimeOffset)] = "TIMESTAMP WITH TIME ZONE",
        [typeof(DateOnly)] = "DATE",
        [typeof(TimeOnly)] = "TIME",
        [typeof(TimeSpan)] = "INTERVAL",
        [typeof(Guid)] = "UUID",
        [typeof(byte[])] = "VARBINARY"
    };

    public virtual string Name => "ANSI";

    public virtual string IdentifierQuotePrefix => "\"";
    public virtual string IdentifierQuoteSuffix => "\"";

    /// <summary>When false, schema names in the model are ignored when generating SQL.</summary>
    public virtual bool SupportsSchemas => true;

    /// <summary>The marker that precedes a named parameter in SQL text (<c>@</c>, <c>:</c>, <c>$</c>).</summary>
    public virtual string ParameterMarker => "@";

    /// <summary>Whether <see cref="DbParameter.ParameterName" /> includes <see cref="ParameterMarker" />.</summary>
    public virtual bool ParameterNameIncludesMarker => true;

    public virtual PagingStyle Paging => PagingStyle.OffsetFetch;

    /// <summary>
    ///     For <see cref="PagingStyle.LimitOffset" />: the LIMIT value meaning "no limit" when only OFFSET is used,
    ///     for databases that reject OFFSET without LIMIT. Null emits a bare OFFSET.
    /// </summary>
    public virtual string? NoLimitValue => null;

    /// <summary>When false, OFFSET/LIMIT values are inlined as literals (the generated SQL is then not cached).</summary>
    public virtual bool SupportsParametersInPaging => true;

    public virtual string TrueLiteral => "TRUE";
    public virtual string FalseLiteral => "FALSE";

    public virtual GeneratedValueRetrieval GeneratedValues => GeneratedValueRetrieval.Returning;

    /// <summary>For <see cref="GeneratedValueRetrieval.SelectAfter" />: e.g. <c>last_insert_rowid()</c>, <c>LAST_INSERT_ID()</c>.</summary>
    public virtual string? IdentityFunction => null;

    /// <summary>For <see cref="GeneratedValueRetrieval.SelectAfter" />: e.g. <c>changes()</c>, <c>ROW_COUNT()</c>.</summary>
    public virtual string? RowsAffectedFunction => null;

    /// <summary>Whether a single DbCommand may contain several <c>;</c>-separated statements.</summary>
    public virtual bool SupportsMultipleStatementsPerCommand => false;

    /// <summary>Maximum commands per SaveChanges batch when the user doesn't set MaxBatchSize. Ignored unless multi-statement commands are supported.</summary>
    public virtual int DefaultMaxBatchSize => 1;

    /// <summary>For <see cref="GeneratedValueRetrieval.RecordsAffected" />: the sequence backing an integer key column.</summary>
    public virtual string GetKeySequenceName(string table, string column)
        => $"{table}_{column}_seq";

    /// <summary>SQL returning one row with the sequence's next value; the name is already delimited.</summary>
    public virtual string GetNextSequenceValueSql(string delimitedSequenceName)
        => "SELECT NEXT VALUE FOR " + delimitedSequenceName;

    public virtual string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";

    public virtual string? GetStoreType(Type clrType)
        => DefaultStoreTypes.GetValueOrDefault(clrType);

    /// <summary>CLR types the provider maps; <see cref="GetStoreType" /> supplies each one's store type.</summary>
    public static IReadOnlyCollection<Type> SupportedClrTypes
        => DefaultStoreTypes.Keys;

    public virtual void Validate()
    {
        if (string.IsNullOrEmpty(ParameterMarker))
        {
            throw new NotSupportedException(
                $"Dialect '{Name}': positional ('?') parameters are not supported; EF Core reuses named parameters within a statement.");
        }

        if (GeneratedValues == GeneratedValueRetrieval.SelectAfter
            && (!SupportsMultipleStatementsPerCommand || IdentityFunction is null || RowsAffectedFunction is null))
        {
            throw new NotSupportedException(
                $"Dialect '{Name}': SelectAfter requires SupportsMultipleStatementsPerCommand, IdentityFunction and RowsAffectedFunction.");
        }
    }

    /// <summary>
    ///     Picks a built-in dialect from the provider factory type, falling back to <see cref="Ansi" />.
    ///     Matching is by type name so this package takes no dependency on any ADO.NET provider.
    /// </summary>
    public static AdoNetSqlDialect Detect(DbProviderFactory? factory)
        => factory?.GetType().FullName switch
        {
            "Microsoft.Data.Sqlite.SqliteFactory" or "System.Data.SQLite.SQLiteFactory" => SqliteSqlDialect.Instance,
            "DuckDB.NET.Data.DuckDBClientFactory" => DuckDbSqlDialect.Instance,
            "pengdows.flatfile.FlatFileProviderFactory" => FlatFileSqlDialect.Instance,
            _ => Ansi
        };
}

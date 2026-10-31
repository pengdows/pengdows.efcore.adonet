using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet;

/// <summary>
///     A database, as a composition root: the parts every database needs (<see cref="Sql" />, <see cref="TypeMappings" />,
///     <see cref="Capabilities" />), plus optional EF Core services (migrations, translations, bulk operations) registered
///     in <see cref="ConfigureServices" />. The core provider never names a dialect; it only consumes this contract.
/// </summary>
/// <remarks>
///     EF Core builds one internal service provider per distinct dialect, as decided by <see cref="object.Equals(object)" />
///     and <see cref="object.GetHashCode" />. Share instances, or give configurable dialects value equality.
/// </remarks>
public interface IAdoNetDialect
{
    string Name { get; }

    ISqlDialect Sql { get; }

    ITypeMappingDialect TypeMappings { get; }

    IDatabaseCapabilities Capabilities { get; }

    /// <summary>
    ///     Registers database-specific EF Core services. Runs before the core registers its defaults, which only fill
    ///     what is still missing, so anything registered here wins. Dialect packages may use their driver's own APIs here.
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Reads the database's schema (reverse engineering); null where the dialect can't.</summary>
    Scaffolding.SchemaReader? SchemaReader { get; }
}

/// <summary>The SQL text: identifiers, parameters, literals, and the grammar parts that differ between databases.</summary>
public interface ISqlDialect
{
    /// <summary>The isolation level to pass to the driver when EF requests its default.</summary>
    System.Data.IsolationLevel DefaultTransactionIsolationLevel { get; }

    /// <summary>
    /// The isolation levels the engine can honour. A transaction asking for any other level is refused with a
    /// NotSupportedException that names it; a level is never silently replaced by a different one.
    /// </summary>
    IReadOnlySet<System.Data.IsolationLevel> SupportedIsolationLevels { get; }

    /// <summary>Whether a suppressed transaction completion must keep the connection open for the caller.</summary>
    bool KeepConnectionOpenAfterSuppressedTransaction { get; }

    string IdentifierQuotePrefix { get; }
    string IdentifierQuoteSuffix { get; }

    /// <summary>
    ///     Run on each database session before EF uses it, to put the session in the mode the dialect writes SQL for (such as
    ///     ISO quoted identifiers); null for none. A session opened outside EF is set up the first time EF uses it.
    /// </summary>
    string? SessionSetup { get; }

    /// <summary>
    ///     Connection string options the dialect's SQL relies on, by key: added to the connection string connections are made
    ///     from and to a closed connection the provider is given; a data source or open connection without them is refused,
    ///     as neither can be changed. Empty for none.
    /// </summary>
    IReadOnlyDictionary<string, string> ConnectionOptions { get; }

    /// <summary>
    ///     How a stored procedure's output parameters reach EF: null where the driver returns them as output parameters of
    ///     the <c>CALL</c>; otherwise a session variable for each, named by this template from <c>{0}</c> (<c>@{0}</c>), which
    ///     is set before the call (to the input, for an input-output parameter), passed in the parameter's place, and
    ///     selected back after it.
    /// </summary>
    string? ProcedureOutputVariable { get; }

    /// <summary>Whether <paramref name="identifier" /> must be quoted to mean itself; quoting every one is always safe.</summary>
    bool RequiresQuoting(string identifier);

    /// <summary>When false, schema names in the model are ignored when generating SQL.</summary>
    bool SupportsSchemas { get; }

    /// <summary>
    ///     Whether the database has stored procedures. Where it hasn't, a model mapping an entity type's inserts, updates or
    ///     deletes to them is refused when the context starts.
    /// </summary>
    bool SupportsStoredProcedures { get; }

    /// <summary>
    ///     Whether the database generates a value for a column of a primary key of several columns (an owned collection's
    ///     own key). Where it can't (a database that generates only a key of one integer column, its row identifier), the
    ///     database refuses a row that leaves such a column to it, as with the database's own EF Core provider: the model gives
    ///     the entity type a key of its own (efcore#26708).
    /// </summary>
    bool GeneratesCompositeKeyValues { get; }

    /// <summary>
    ///     Whether a derived table (a subquery in FROM) may read a column of a table outside it: an outer reference (ISO
    ///     9075-2 6.7). Where it may not, nothing in a derived table reads outside it at any depth, and a query EF translates
    ///     to one that does is refused; a subquery that isn't in FROM, or a table function in FROM, still reads the outer row.
    /// </summary>
    bool SupportsOuterReferencesInDerivedTables { get; }

    /// <summary>
    ///     Whether the database has ISO SQL:2003 window functions (<c>ROW_NUMBER() OVER (...)</c>), which EF writes for the
    ///     first rows of each group; where it hasn't (an older server), a query EF translates to one is refused. True by
    ///     default.
    /// </summary>
    bool SupportsWindowFunctions => true;

    /// <summary>The marker that precedes a named parameter in SQL text (<c>@</c>, <c>:</c>, <c>$</c>).</summary>
    string ParameterMarker { get; }

    /// <summary>Whether <see cref="System.Data.Common.DbParameter.ParameterName" /> includes <see cref="ParameterMarker" />.</summary>
    bool ParameterNameIncludesMarker { get; }

    /// <summary>A string constant as SQL text, quotes and any other characters the database treats specially escaped.</summary>
    string StringLiteral(string value);

    string TrueLiteral { get; }
    string FalseLiteral { get; }

    /// <summary>The aggregate for <c>LongCount</c>, for databases whose <c>COUNT</c> returns a 32-bit integer.</summary>
    string LongCountFunction { get; }

    PagingSyntax Paging { get; }
    BooleanSyntax Booleans { get; }
    KeyRetrieval Keys { get; }

    /// <summary>
    ///     How keys come back from a table annotated <see cref="AdoNetAnnotationNames.UseReturningClause" /> false, one that
    ///     can't take <see cref="Keys" />' RETURNING clause (a table with triggers, or a virtual table); null where every table
    ///     takes it.
    /// </summary>
    SelectAfterKeyRetrieval? KeysWithoutReturning { get; }

    /// <summary>
    ///     The annotation that turns RETURNING off for a table, named by the dialect where it names one;
    ///     <see cref="AdoNetAnnotationNames.UseReturningClause" /> where the dialect names none.
    /// </summary>
    string ReturningClauseAnnotation => AdoNetAnnotationNames.UseReturningClause;

    /// <summary>How <c>ExecuteDelete</c>/<c>ExecuteUpdate</c> are written.</summary>
    DmlSyntax Dml { get; }

    /// <summary>
    ///     Where <c>x IN (subquery)</c> over nullable values would need COALESCE and CASE to keep .NET's semantics, write
    ///     <c>EXISTS</c> instead, for databases where it costs no more and reads better.
    /// </summary>
    bool PreferExistsToIn { get; }

    /// <summary>How the functions that .NET members translate to are spelled.</summary>
    FunctionSyntax Functions { get; }

    /// <summary>What follows <c>INSERT INTO t</c> for a row with no columns to write: ISO's <c>DEFAULT VALUES</c>.</summary>
    string DefaultValues { get; }

    /// <summary>
    ///     An expression for the schema unqualified names resolve to (<c>CURRENT_SCHEMA</c>), so a lookup by name alone stays
    ///     there; null when the database can't say, and the name is then looked up in every schema.
    /// </summary>
    string? CurrentSchema { get; }

    /// <summary>
    ///     The database's savepoints, as SQL for drivers whose transactions don't offer them; null when the database has none.
    ///     This decides whether there are savepoints, not the driver: <c>DbTransaction.SupportsSavepoints</c> says whether the
    ///     driver implements the methods, and a driver may report them on a server that ignores them.
    /// </summary>
    SavepointSyntax? Savepoints { get; }

    /// <summary>
    ///     How an INSERT writes an application-set value into a generated key column; null where the database takes one as
    ///     it takes any other value.
    /// </summary>
    IdentityInsertSyntax? IdentityInsert => null;

    /// <summary>How a database is created and deleted; null when the dialect can do neither.</summary>
    DatabaseLifecycle? Databases { get; }

    /// <summary>JSON columns and the functions over them; null when the dialect has none.</summary>
    JsonSyntax? Json { get; }

    /// <summary>Spatial columns and their functions (with the NetTopologySuite package); null when the dialect has none.</summary>
    SpatialSyntax? Spatial { get; }

    /// <summary>Whether tuple comparisons can be written as ISO row values, <c>(a, b) = (x, y)</c>.</summary>
    bool RowValues { get; }

    /// <summary>
    ///     Written before each row of a table value constructor: empty for ISO's <c>VALUES (1), (2)</c>, <c>ROW</c> where the
    ///     database writes <c>VALUES ROW(1), ROW(2)</c>.
    /// </summary>
    string ValuesRowPrefix { get; }

    /// <summary>
    ///     Whether an <c>IN (subquery)</c> may page its subquery. Where it can't, the subquery is read through a derived
    ///     table, which may page: <c>x IN (SELECT t.c FROM (subquery) AS t)</c> (an uncorrelated subquery only).
    /// </summary>
    bool LimitInPredicateSubqueries { get; }

    /// <summary>
    ///     How a correlated subquery is joined to the rows before it; null where the database has no lateral join, and a query
    ///     needing one fails to translate.
    /// </summary>
    LateralJoinSyntax? LateralJoins { get; }

    /// <summary>Whether every expression projected by a derived table must have an explicit column name.</summary>
    bool RequiresDerivedTableColumnAliases { get; }

    /// <summary>Whether the database accepts a native <c>(VALUES (...), (...)) AS alias(column)</c> table expression.</summary>
    bool UsesNativeValuesTable { get; }

    /// <summary>Whether ORDER BY accepts <c>NULLS FIRST</c> and <c>NULLS LAST</c>, which the null-ordering option writes.</summary>
    bool SupportsNullOrderingClauses => false;

    /// <summary>A cast a parameter of this store type needs after its placeholder, for a dialect whose driver sends it as text; null when none.</summary>
    string? ParameterCast(string storeType) => null;

    /// <summary>Returns the number of user tables in the current database or schema.</summary>
    string HasTablesSql { get; }
}

/// <summary>Store type names; the core binds every type through the portable <see cref="System.Data.DbType" />.</summary>
public interface ITypeMappingDialect
{
    /// <summary>The store type for one of <see cref="AnsiTypeMappings.SupportedClrTypes" />, or null when the database has none.</summary>
    string? GetStoreType(Type clrType);

    /// <summary>
    ///     The store type with the model's facets, e.g. from <c>[MaxLength]</c>, <c>IsFixedLength</c>, <c>IsUnicode</c> or
    ///     <c>HasPrecision</c>; null when they don't change it.
    /// </summary>
    string? GetStoreType(Type clrType, StoreTypeFacets facets);

    /// <summary>
    ///     The CLR types a store type holds without conversion, the default first; empty when the name isn't known.
    ///     <paramref name="storeTypeBase" /> has no facets: <c>varchar</c>, not <c>varchar(30)</c>.
    /// </summary>
    IReadOnlyList<Type> GetClrTypes(string storeTypeBase);

    /// <summary>
    ///     The store type of a row version of <paramref name="clrType" /> (<c>[Timestamp]</c>, <c>IsRowVersion</c>): a column
    ///     the database sets on every insert and update. Null where it has none for that type.
    /// </summary>
    string? RowVersionStoreType(Type clrType)
        => null;

    /// <summary>
    ///     The size given to a string parameter for an unbounded column when its value fits, so queries don't vary with the
    ///     values' lengths; null leaves the size to the driver.
    /// </summary>
    int? StringParameterSize(bool unicode);

    /// <summary>
    ///     A constant as SQL text. <paramref name="literal" /> is EF's ISO form (<c>TIMESTAMP '...'</c>, <c>X'...'</c>, plain
    ///     numbers); return it, or the database's own. Strings and booleans come from <see cref="ISqlDialect" /> instead.
    /// </summary>
    string FormatLiteral(object value, string storeType, string literal);

    /// <summary>
    ///     What <c>CAST(x AS ...)</c> names for a value of <paramref name="storeType" />: the store type itself in ISO SQL; a
    ///     database whose CAST takes only its own target names (SIGNED, CHAR...) maps to them.
    /// </summary>
    string CastTarget(string storeType);

    /// <summary>
    ///     How a value <c>{0}</c> is converted to <paramref name="storeType" />: <c>CAST({0} AS </c><see cref="CastTarget" /><c>)</c>,
    ///     unless the database converts better another way.
    /// </summary>
    string Cast(string storeType);

    /// <summary>
    ///     The DbType a DateTime parameter binds as: DateTime2 (a date and time with no offset) unless the database has one
    ///     datetime type its drivers bind as DbType.DateTime.
    /// </summary>
    System.Data.DbType DateTimeDbType { get; }

    /// <summary>
    ///     The DbType a parameter of <paramref name="clrType" /> binds as, given EF's <paramref name="standard" /> one: that one,
    ///     unless the driver converts it wrongly where a statement doesn't fix the parameter's type (one sends a Decimal as the
    ///     current culture's text; one makes a Time-typed TimeSpan a time of day, refusing a negative one).
    /// </summary>
    System.Data.DbType ParameterDbType(Type clrType, System.Data.DbType standard)
        => standard;

    /// <summary>
    ///     Whether the driver writes a DateTimeOffset parameter only at offset zero, so one is converted to UTC (the same
    ///     instant) before it binds. False where the driver takes any offset.
    /// </summary>
    bool WritesDateTimeOffsetAsUtc => false;
}

/// <summary>The facets of a property that can change its store type.</summary>
/// <param name="Size">The length of a string or binary column.</param>
/// <param name="Precision">The precision of a decimal (or a time with fractional seconds).</param>
/// <param name="Scale">The scale of a decimal.</param>
/// <param name="FixedLength">Whether a string or binary column is padded to its length.</param>
/// <param name="Unicode">Whether a string column holds Unicode text; true unless the model says otherwise.</param>
/// <param name="KeyOrIndex">
///     Whether the column is in a key or an index, where some databases need a bounded type for a string or binary column.
/// </param>
public sealed record StoreTypeFacets(
    int? Size = null, int? Precision = null, int? Scale = null, bool FixedLength = false, bool Unicode = true, bool KeyOrIndex = false);

/// <summary>What the driver accepts, as opposed to what the SQL looks like.</summary>
public interface IDatabaseCapabilities
{
    /// <summary>Whether a single DbCommand may contain several <c>;</c>-separated statements.</summary>
    bool SupportsMultipleStatementsPerCommand { get; }

    /// <summary>Maximum commands per SaveChanges batch when the user doesn't set MaxBatchSize. Ignored unless multi-statement commands are supported.</summary>
    int DefaultMaxBatchSize { get; }

    /// <summary>The most parameters one command may carry (bulk loads size their INSERTs by it).</summary>
    int MaxParametersPerCommand { get; }

    /// <summary>
    ///     The longest identifier the database takes. Names EF makes up (constraints, indexes, columns of owned types) are
    ///     shortened to fit, uniquely.
    /// </summary>
    int MaxIdentifierLength { get; }

    /// <summary>
    ///     Whether the driver's connections enlist in an ambient <c>System.Transactions</c> transaction (a
    ///     <c>TransactionScope</c>). Where they don't, EF warns that one is active.
    /// </summary>
    bool SupportsAmbientTransactions { get; }

    /// <summary>
    ///     Whether one connection can hold several open data readers at once. Where it can't, a split query's results are
    ///     buffered, so each command runs after the previous reader is read and closed.
    /// </summary>
    bool SupportsMultipleActiveReaders { get; }

    /// <summary>
    ///     <see cref="SupportsMultipleActiveReaders" /> for connections made from <paramref name="connectionString" />, where a
    ///     driver lets each connection choose (null when the context has none yet). Queries compiled for one answer aren't
    ///     reused for the other.
    /// </summary>
    bool SupportsMultipleActiveReadersFor(string? connectionString)
        => SupportsMultipleActiveReaders;

    /// <summary>
    ///     Whether the driver hands a binary value back only as a stream, refusing <c>GetFieldValue&lt;byte[]&gt;</c>. Its readers
    ///     then copy each binary value out while its row is current.
    /// </summary>
    bool ReadsBinaryAsStream
        => false;

    /// <summary>
    ///     Whether a failure may succeed if retried (a deadlock, a dropped connection, a busy server), for a retrying
    ///     execution strategy (<c>EnableRetryOnFailure</c>).
    /// </summary>
    bool IsTransient(Exception exception);
}

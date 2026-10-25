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
}

/// <summary>The SQL text: identifiers, parameters, literals, and the grammar parts that differ between databases.</summary>
public interface ISqlDialect
{
    string IdentifierQuotePrefix { get; }
    string IdentifierQuoteSuffix { get; }

    /// <summary>When false, schema names in the model are ignored when generating SQL.</summary>
    bool SupportsSchemas { get; }

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

    /// <summary>How <c>ExecuteDelete</c>/<c>ExecuteUpdate</c> are written.</summary>
    DmlSyntax Dml { get; }

    /// <summary>
    ///     Where <c>x IN (subquery)</c> over nullable values would need COALESCE and CASE to keep .NET's semantics, write
    ///     <c>EXISTS</c> instead (as SQL Server's provider does: it costs no more there and reads better).
    /// </summary>
    bool PreferExistsToIn { get; }

    /// <summary>How the functions that .NET members translate to are spelled.</summary>
    FunctionSyntax Functions { get; }

    /// <summary>Returns the number of user tables in the current database or schema.</summary>
    string HasTablesSql { get; }
}

/// <summary>Store type names; the core binds every type through the portable <see cref="System.Data.DbType" />.</summary>
public interface ITypeMappingDialect
{
    /// <summary>The store type for one of <see cref="AnsiTypeMappings.SupportedClrTypes" />, or null when the database has none.</summary>
    string? GetStoreType(Type clrType);

    /// <summary>
    ///     The store type with a length (strings, binary) or precision and scale (decimals) from the model, e.g. from
    ///     <c>[MaxLength]</c>, <c>IsFixedLength</c> or <c>HasPrecision</c>; null when facets don't change it.
    /// </summary>
    string? GetStoreType(Type clrType, int? size, int? precision, int? scale, bool fixedLength);

    /// <summary>
    ///     The CLR types a store type holds without conversion, the default first; empty when the name isn't known.
    ///     <paramref name="storeTypeBase" /> has no facets: <c>varchar</c>, not <c>varchar(30)</c>.
    /// </summary>
    IReadOnlyList<Type> GetClrTypes(string storeTypeBase);

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
}

/// <summary>What the driver accepts, as opposed to what the SQL looks like.</summary>
public interface IDatabaseCapabilities
{
    /// <summary>Whether a single DbCommand may contain several <c>;</c>-separated statements.</summary>
    bool SupportsMultipleStatementsPerCommand { get; }

    /// <summary>Maximum commands per SaveChanges batch when the user doesn't set MaxBatchSize. Ignored unless multi-statement commands are supported.</summary>
    int DefaultMaxBatchSize { get; }

    /// <summary>The most parameters one command may carry (bulk loads size their INSERTs by it).</summary>
    int MaxParametersPerCommand { get; }
}

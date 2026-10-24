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

    string TrueLiteral { get; }
    string FalseLiteral { get; }

    /// <summary>The aggregate for <c>LongCount</c>, for databases whose <c>COUNT</c> returns a 32-bit integer.</summary>
    string LongCountFunction { get; }

    PagingSyntax Paging { get; }
    BooleanSyntax Booleans { get; }
    KeyRetrieval Keys { get; }

    /// <summary>How <c>ExecuteDelete</c>/<c>ExecuteUpdate</c> are written.</summary>
    DmlSyntax Dml { get; }

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
    ///     <c>[MaxLength]</c> or <c>HasPrecision</c>; null when facets don't change it.
    /// </summary>
    string? GetStoreType(Type clrType, int? size, int? precision, int? scale);
}

/// <summary>What the driver accepts, as opposed to what the SQL looks like.</summary>
public interface IDatabaseCapabilities
{
    /// <summary>Whether a single DbCommand may contain several <c>;</c>-separated statements.</summary>
    bool SupportsMultipleStatementsPerCommand { get; }

    /// <summary>Maximum commands per SaveChanges batch when the user doesn't set MaxBatchSize. Ignored unless multi-statement commands are supported.</summary>
    int DefaultMaxBatchSize { get; }
}

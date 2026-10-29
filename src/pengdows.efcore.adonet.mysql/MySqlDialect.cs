using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL. Identifiers are quoted MySQL's own way, backticks, so no session-level mode is needed to make them mean what
///     this dialect's SQL assumes. A database is the schema, OFFSET needs a LIMIT, and keys come back through
///     <c>LAST_INSERT_ID()</c>. GUIDs are stored as <c>CHAR(36)</c> text.
/// </summary>
public sealed partial class MySqlDialect : AdoNetDialect
{
    /// <summary>For the common case: the server's sql_mode does not have NO_BACKSLASH_ESCAPES.</summary>
    public static readonly MySqlDialect Instance = new(backslashEscapes: true);

    /// <summary>
    ///     For a server whose sql_mode has NO_BACKSLASH_ESCAPES, where a backslash in a string is just a backslash: nothing
    ///     reads the server's mode, so use this where it does, <see cref="Instance" /> where it doesn't.
    /// </summary>
    public static readonly MySqlDialect NoBackslashEscapes = new(backslashEscapes: false);

    /// <summary>
    ///     SingleStore, which speaks MySQL but accepts <c>SAVEPOINT</c> and <c>ROLLBACK TO SAVEPOINT</c> and ignores them, so
    ///     work rolled back to a savepoint would be committed: this dialect has no savepoints. MySqlConnector reports them
    ///     there anyway, since its <c>SupportsSavepoints</c> describes the driver, not the server
    ///     (https://github.com/mysql-net/MySqlConnector/issues/1677).
    /// </summary>
    public static readonly MySqlDialect SingleStore = new(backslashEscapes: true, savepoints: false, name: "SingleStore");

    /// <summary>
    ///     MariaDB, its own engine since 10.0: no LATERAL derived tables (https://jira.mariadb.org/browse/MDEV-19078), so a
    ///     query needing one is refused; a table value constructor's rows without <c>ROW</c>; and JSON text made JSON through
    ///     a JSON function, since its JSON is an alias of LONGTEXT and there is no JSON type to CAST to.
    /// </summary>
    public static readonly MySqlDialect MariaDb = new(backslashEscapes: true, name: "MariaDB", mariaDb: true);

    private MySqlDialect(bool backslashEscapes, bool savepoints = true, string? name = null, bool mariaDb = false, Version? serverVersion = null)
    {
        Name = name ?? (backslashEscapes ? "MySQL" : "MySQL (NO_BACKSLASH_ESCAPES)");
        ServerVersion = serverVersion;
        Sql = new MySqlSql(backslashEscapes, savepoints, mariaDb, MySqlServerFeatures.For(mariaDb, serverVersion));
    }

    public override string Name { get; }

    /// <summary>The server version this dialect was made for (<see cref="ForServerVersion" />); null for a current server.</summary>
    public Version? ServerVersion { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Version, bool), MySqlDialect> Versions = new();

    /// <summary>
    ///     A dialect for a MySQL or MariaDB server of <paramref name="version" />, which turns off what that server lacks
    ///     (LATERAL, window functions, JSON_TABLE, the VALUES table constructor, REGEXP_LIKE, expression defaults, a column's
    ///     SRID, DESC indexes, RENAME COLUMN ...), as Pomelo's <c>MySqlServerVersion</c> and <c>MariaDbServerVersion</c> do.
    ///     A server that has them all gets <see cref="Instance" /> or <see cref="MariaDb" />; the same version gets the same
    ///     instance.
    /// </summary>
    /// <exception cref="NotSupportedException">The server is older than MySQL 5.7.8 or MariaDB 10.2.4, whose JSON functions this dialect writes.</exception>
    public static MySqlDialect ForServerVersion(Version version, bool mariaDb = false)
    {
        var oldest = mariaDb ? MySqlServerFeatures.OldestMariaDb : MySqlServerFeatures.OldestMySql;
        if (version < oldest)
        {
            throw new NotSupportedException($"{(mariaDb ? "MariaDB" : "MySQL")} {version} is older than {oldest}, the oldest this dialect writes SQL for.");
        }

        if (MySqlServerFeatures.For(mariaDb, version) == MySqlServerFeatures.For(mariaDb, null))
        {
            return mariaDb ? MariaDb : Instance;
        }

        return Versions.GetOrAdd(
            (version, mariaDb),
            key => new MySqlDialect(backslashEscapes: true, name: key.Item2 ? "MariaDB" : "MySQL", mariaDb: key.Item2, serverVersion: key.Item1));
    }
    public override ISqlDialect Sql { get; }
    public override ITypeMappingDialect TypeMappings { get; } = new MySqlTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
        MaxParametersPerCommand = 65535,
        // MySQL's limit for table, column, index and constraint names.
        MaxIdentifierLength = 64,
        // The driver enlists in a TransactionScope. Behaviour reference (not copied):
        // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlRelationalConnection.cs
        SupportsAmbientTransactions = true
    };

    public override pengdows.efcore.adonet.Scaffolding.SchemaReader SchemaReader
        => ((MySqlSql)Sql).IsMariaDb ? MySqlSchemaReader.MariaDb : MySqlSchemaReader.Instance;

    /// <summary>
    ///     <see cref="Instance" /> or <see cref="MariaDb" />, from <paramref name="connection" />'s own
    ///     <see cref="System.Data.Common.DbConnection.ServerVersion" />: every driver already reads this from the connection
    ///     handshake, with no query of its own, so there is nothing to open or close here. As Pomelo's <c>ServerVersion.Parse</c>
    ///     tells them apart (not copied): MariaDB's version string names itself; MySQL's never does.
    ///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/1217f926be3771c016eb26b8192119569902d215/src/EFCore.MySql/Infrastructure/ServerVersion.cs
    ///     This alone cannot tell <see cref="SingleStore" /> apart: its handshake deliberately reports a MySQL-compatible
    ///     version to stay compatible with version-gated clients (confirmed against a real server: <c>VERSION()</c> reads
    ///     <c>5.7.32</c>). <see cref="AutoDetect(DbProviderFactory, string)" /> tells it apart with one extra query; this
    ///     overload alone cannot, and returns a MySQL dialect for it. The version in the handshake goes to
    ///     <see cref="ForServerVersion" />, so an older server gets a dialect that turns off what it lacks.
    /// </summary>
    public static MySqlDialect FromServerVersion(DbConnection connection)
    {
        var handshake = connection.ServerVersion;
        var mariaDb = handshake.Contains("mariadb", StringComparison.OrdinalIgnoreCase);
        return HandshakeVersion(handshake, mariaDb) is { } version ? ForServerVersion(version, mariaDb) : mariaDb ? MariaDb : Instance;
    }

    // MySQL's handshake leads with its version ("8.0.36-log"); MariaDB's names itself after its own, which may follow the
    // 5.5.5- that old clients need ("5.5.5-10.11.6-MariaDB"). Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Infrastructure/ServerVersion.cs
    private static Version? HandshakeVersion(string handshake, bool mariaDb)
    {
        var match = mariaDb ? MariaDbVersion().Match(handshake) : MySqlVersion().Match(handshake);
        return match.Success ? Version.Parse(match.Groups[1].Value) : null;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(\d+\.\d+\.\d+)-MariaDB", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex MariaDbVersion();

    [System.Text.RegularExpressions.GeneratedRegex(@"^(\d+\.\d+\.\d+)")]
    private static partial System.Text.RegularExpressions.Regex MySqlVersion();

    /// <summary>
    ///     <see cref="Instance" />, <see cref="MariaDb" /> or <see cref="SingleStore" />: <paramref name="versionComment" />
    ///     is <c>@@version_comment</c>, the one signal that tells SingleStore apart from a real MySQL or MariaDB server, since
    ///     its handshake version does not. Confirmed against a real server: <c>@@version_comment</c> there reads
    ///     <c>SingleStoreDB source distribution (compatible; MySQL Enterprise &amp; MySQL Commercial)</c>.
    /// </summary>
    public static MySqlDialect FromServerVersion(DbConnection connection, string versionComment)
        => versionComment.Contains("singlestore", StringComparison.OrdinalIgnoreCase) ? SingleStore : FromServerVersion(connection);

    /// <summary>
    ///     <see cref="FromServerVersion(DbConnection, string)" />, opening <paramref name="connectionString" /> with
    ///     <paramref name="factory" /> and running <c>SELECT @@version_comment</c> once, as Pomelo's
    ///     <c>ServerVersion.AutoDetect</c> opens a connection for its own handshake-only detection (not copied). Whichever
    ///     driver the application goes on to use, including a different one than <paramref name="factory" />, this reads the
    ///     same values.
    /// </summary>
    public static MySqlDialect AutoDetect(DbProviderFactory factory, string connectionString)
    {
        using var connection = CreateConnection(factory, connectionString);
        connection.Open();
        return FromServerVersion(connection, VersionComment(connection));
    }

    /// <summary><see cref="AutoDetect(DbProviderFactory, string)" />, without blocking on the connection.</summary>
    public static async Task<MySqlDialect> AutoDetectAsync(DbProviderFactory factory, string connectionString, CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection(factory, connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return FromServerVersion(connection, await VersionCommentAsync(connection, cancellationToken).ConfigureAwait(false));
        }
    }

    private static string VersionComment(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT @@version_comment";
        return command.ExecuteScalar() as string ?? "";
    }

    private static async Task<string> VersionCommentAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = "SELECT @@version_comment";
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string ?? "";
        }
    }

    private static DbConnection CreateConnection(DbProviderFactory factory, string connectionString)
    {
        var connection = factory.CreateConnection()
            ?? throw new InvalidOperationException($"{factory.GetType()} did not create a connection.");
        connection.ConnectionString = connectionString;
        return connection;
    }

    public override void ConfigureServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, MySqlMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, MySqlHistoryRepository>()
            .TryAdd<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor, MySqlDataValueReaderInterceptor>()
            .TryAdd<Microsoft.EntityFrameworkCore.Metadata.IRelationalAnnotationProvider, MySqlAnnotationProvider>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IMethodCallTranslatorPlugin, MySqlFullTextTranslatorPlugin>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IMethodCallTranslatorPlugin, MySqlJsonTranslatorPlugin>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IMethodCallTranslatorPlugin, MySqlDbFunctionsTranslatorPlugin>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IEvaluatableExpressionFilterPlugin, MySqlEvaluatableExpressionFilterPlugin>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IQueryTranslationPreprocessorFactory, MySqlQueryTranslationPreprocessorFactory>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IQuerySqlGeneratorFactory, MySqlQuerySqlGeneratorFactory>()
            .TryAdd<Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure.IConventionSetPlugin, MySqlAttributeConventionPlugin>();
}

internal sealed class MySqlSql(bool backslashEscapes, bool savepoints, bool mariaDb, MySqlServerFeatures features) : AnsiSqlDialect
{
    private const string MariaDbDateTimeOffsetFromText =
        "CASE WHEN {0} REGEXP '[+-][0-9][0-9]:[0-9][0-9]$' THEN CAST(LEFT({0}, CHAR_LENGTH({0}) - 6) AS DATETIME(6)) - INTERVAL "
        + "((CASE SUBSTRING({0}, -6, 1) WHEN '-' THEN -1 ELSE 1 END) * (SUBSTRING({0}, -5, 2) * 60 + SUBSTRING({0}, -2, 2))) MINUTE "
        + "ELSE CAST({0} AS DATETIME(6)) END";

    // MariaDB's DDL differs from MySQL's where its migrations write it (a column's REF_SYSTEM_ID for MySQL's SRID).
    internal bool IsMariaDb => mariaDb;

    // What the server of the dialect's version has (all of it for a current server).
    internal MySqlServerFeatures Features => features;

    public override IsolationLevel DefaultTransactionIsolationLevel => IsolationLevel.ReadCommitted;
    public override bool KeepConnectionOpenAfterSuppressedTransaction => true;

    // Through a connection to no database. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlDatabaseCreator.cs
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Server(
        "Database",
        adminDatabase: null);

    public override SavepointSyntax? Savepoints
        => savepoints ? SavepointSyntax.Iso : null;

    // MySQL's json type and functions. JSON_UNQUOTE turns a JSON null into the text "null", so a string is read only when the
    // value isn't null; a bool compares against JSON true and false; typed values unquote and CAST the extracted scalar.
    // JSON_VALUE's RETURNING form does not accept a computed path expression on MySQL, while JSON_EXTRACT does.
    // A patch value given as JSON text is CAST to JSON; MariaDB has no JSON type to CAST to, and takes a JSON function's
    // result as JSON.
    public override JsonSyntax Json { get; } = new(
        "json",
        "CASE JSON_TYPE(JSON_EXTRACT({0}, {1})) WHEN 'NULL' THEN NULL ELSE JSON_UNQUOTE(JSON_EXTRACT({0}, {1})) END",
        "JSON_EXTRACT({0}, {1})",
        // A number or boolean is read as JSON true/false or 1/0: a bool stored as text goes through its converter instead.
        "CASE JSON_TYPE(JSON_EXTRACT({0}, {1})) WHEN 'NULL' THEN NULL ELSE "
        + "CASE LOWER(JSON_UNQUOTE(JSON_EXTRACT({0}, {1}))) WHEN 'true' THEN CAST(1 AS {2}) WHEN 'false' THEN CAST(0 AS {2}) "
        + "ELSE CAST(JSON_UNQUOTE(JSON_EXTRACT({0}, {1})) AS {2}) END END",
        "CASE LOWER(JSON_UNQUOTE(JSON_EXTRACT({0}, {1}))) WHEN 'true' THEN TRUE WHEN '1' THEN TRUE "
        + "WHEN 'false' THEN FALSE WHEN '0' THEN FALSE END",
        "FROM_BASE64(JSON_UNQUOTE(JSON_EXTRACT({0}, {1})))",
        "CONCAT({0}, {1})",
        "JSON_SET({0}, {1}, {2})",
        mariaDb ? "JSON_COMPACT({0})" : "CAST({0} AS JSON)")
    {
        // FOR ORDINALITY numbers the elements in array order.
        // Before JSON_TABLE (MySQL 8.0.4, MariaDB 10.6) a primitive collection is queried only as a whole.
        ArrayElements = features.JsonTable ? "JSON_TABLE({0}, '$[*]' COLUMNS ({1} FOR ORDINALITY, {2} {3} PATH '$'))" : null,

        // A JSON_TABLE text column takes the connection's collation as strongly as a table column takes its own, so comparing the
        // two fails when they differ ("Illegal mix of collations"); a _bin collation yields to neither and compares ordinally,
        // as .NET does.
        ArrayElementsTextType = "{0} CHARACTER SET utf8mb4 COLLATE utf8mb4_bin",
        ArrayElementsBinaryValue = "FROM_BASE64({0})",

        // An IN or EXISTS subquery turned into a semijoin, or a lateral derived table merged into its query, over a JSON_TABLE of
        // an outer column reads the column as empty ("The document is empty"); the semijoin case is fixed in 9.3.0
        // (https://bugs.mysql.com/bug.php?id=114897, UPSTREAM_ISSUES.md).
        ArrayElementsHint = "/*+ NO_SEMIJOIN() NO_MERGE() */",

        // MariaDB's JSON is LONGTEXT, so = compares spellings (its JSON functions write "[1, 2]", EF writes "[1,2]").
        // Before JSON_EQUALS (MariaDB 10.7) the documents are compared compacted: the same whitespace, not the same key order.
        Equality = !mariaDb ? null : features.JsonEquals ? "JSON_EQUALS({0}, {1})" : "JSON_COMPACT({0}) = JSON_COMPACT({1})",

        // MariaDB's CAST of JSON's "2000-01-01T12:34:56-08:00" to DATETIME, and its JSON_TABLE, ignore the offset; MySQL applies
        // it. The offset is subtracted in minutes (CONVERT_TZ only converts within the TIMESTAMP range); text without one is
        // cast as it is.
        DateTimeOffsetFromText = mariaDb ? MariaDbDateTimeOffsetFromText : null
    };

    // MySQL's own quoting, so no session-level mode is needed before this dialect's SQL means what it says.
    public override string IdentifierQuotePrefix => "`";
    public override string IdentifierQuoteSuffix => "`";
    public override bool SupportsSchemas => false;

    // Geometry columns hold MySQL's own format, a four-byte SRID before the WKB, which the drivers pass as bytes. Constants are
    // read with x as longitude whatever the SRID. MySQL 8 has no boundary, point-on-surface or relate, which MariaDB has
    // (https://mariadb.com/kb/en/geometry-properties/); a ring is a closed simple line on both, as Pomelo emulates it where
    // ST_IsRing is missing (MariaDB's is TRUE for NULL). Both keep two dimensions only. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/tree/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.NTS
    public override SpatialSyntax Spatial { get; } = new()
    {
        Binary = SpatialBinary.SridPrefixedWkb,
        StoresZAndM = false,
        // MariaDB has no axis-order option: its x is always the longitude.
        // MySQL before 8.0.1 takes no options, and has no geographic axis order to set.
        FromText = features.GeometryAxisOrder ? "ST_GeomFromText({0}, {1}, 'axis-order=long-lat')" : "ST_GeomFromText({0}, {1})",
        Functions = new Dictionary<string, string?>
        {
            // MariaDB has only the plural name, and no validity test.
            ["NumInteriorRings"] = "ST_NumInteriorRings({0})",
            ["IsValid"] = mariaDb ? null : "ST_IsValid({0})",
            ["Boundary"] = mariaDb ? "ST_Boundary({0})" : null,
            ["PointOnSurface"] = mariaDb ? "ST_PointOnSurface({0})" : null,
            ["InteriorPoint"] = mariaDb ? "ST_PointOnSurface({0})" : null,
            ["Relate"] = mariaDb ? "ST_Relate({0}, {1}, {2})" : null,
            // MariaDB answers these for a NULL geometry with -1, its error value, which reads as true: NULL is the answer.
            ["IsClosed"] = mariaDb ? "CASE WHEN {0} IS NULL THEN NULL ELSE ST_IsClosed({0}) END" : "ST_IsClosed({0})",
            ["IsSimple"] = mariaDb ? "CASE WHEN {0} IS NULL THEN NULL ELSE ST_IsSimple({0}) END" : "ST_IsSimple({0})",
            ["IsRing"] = mariaDb
                ? "CASE WHEN {0} IS NULL THEN NULL ELSE (ST_IsClosed({0}) AND ST_IsSimple({0})) END"
                : "(ST_IsClosed({0}) AND ST_IsSimple({0}))",
            ["Z"] = null,
            ["M"] = null,
            ["GeometryType"] = "CASE ST_GeometryType({0}) WHEN 'POINT' THEN 'Point' WHEN 'LINESTRING' THEN 'LineString' "
                + "WHEN 'POLYGON' THEN 'Polygon' WHEN 'MULTIPOINT' THEN 'MultiPoint' WHEN 'MULTILINESTRING' THEN 'MultiLineString' "
                + "WHEN 'MULTIPOLYGON' THEN 'MultiPolygon' WHEN 'GEOMCOLLECTION' THEN 'GeometryCollection' END",
            ["OgcGeometryType"] = "CASE ST_GeometryType({0}) WHEN 'POINT' THEN 1 WHEN 'LINESTRING' THEN 2 WHEN 'POLYGON' THEN 3 "
                + "WHEN 'MULTIPOINT' THEN 4 WHEN 'MULTILINESTRING' THEN 5 WHEN 'MULTIPOLYGON' THEN 6 WHEN 'GEOMCOLLECTION' THEN 7 END"
        }
    };
    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset(noLimitValue: "18446744073709551615").WithValueLimitsOnly();
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("LAST_INSERT_ID()", "ROW_COUNT()");
    public override DmlSyntax Dml => DmlSyntax.MultiTable;
    public override FunctionSyntax Functions { get; } = new MySqlFunctions(mariaDb, features);
    public override string DefaultValues => "() VALUES ()";
    public override string ValuesRowPrefix => (mariaDb || !features.TableValueConstructor) ? "" : "ROW";
    public override LateralJoinSyntax? LateralJoins => features.LateralJoins ? LateralJoinSyntax.Iso : null;

    // MySQL before 8.0, MariaDB before 10.2.
    public override bool SupportsWindowFunctions => features.WindowFunctions;

    // MySQL reads an outer column in a derived table since 8.0.14; MariaDB doesn't (its "Unknown column" in WHERE), as
    // Pomelo's OuterReferenceInMultiLevelSubquery says. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Infrastructure/MariaDbServerVersion.cs
    public override bool SupportsOuterReferencesInDerivedTables => features.OuterReferencesInDerivedTables;

    // Neither driver returns output parameters from a text CALL: each output goes through a user variable, which both
    // drivers take for a missing parameter unless the connection allows user variables. As Pomelo does. Behaviour reference
    // (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlConnectionStringOptionsValidator.cs
    public override string ProcedureOutputVariable => "@{0}";
    public override IReadOnlyDictionary<string, string> ConnectionOptions { get; } = new Dictionary<string, string>
    {
        ["Allow User Variables"] = "true",
        ["Use Affected Rows"] = "false"
    };
    public override string CurrentSchema => "DATABASE()";

    // "This version of MySQL doesn't yet support 'LIMIT & IN/ALL/ANY/SOME subquery'"; a derived table may be paged.
    public override bool LimitInPredicateSubqueries => false;

    // Backslash is an escape character in MySQL string literals, unless the server's sql_mode has NO_BACKSLASH_ESCAPES: pick
    // the dialect instance that matches the server's actual mode, since nothing here changes it.
    public override string StringLiteral(string value)
        => base.StringLiteral(backslashEscapes ? value.Replace(@"\", @"\\") : value);

    // INFORMATION_SCHEMA.TABLES spans every database on the server.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = DATABASE()";
}

/// <summary>
///     <c>||</c> means OR. FLOOR and CEILING of a DECIMAL return BIGINT; adding 0.0 keeps the result a decimal. The SECOND
///     part is already whole, and CAST has no INTEGER target.
/// </summary>
internal sealed class MySqlFunctions(bool mariaDb, MySqlServerFeatures features) : FunctionSyntax
{
    // TIMESTAMPDIFF counts whole units; it has no MILLISECOND, TICK or NANOSECOND, so microseconds are divided or multiplied.
    // Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/Internal/MySqlDateDiffFunctionsTranslator.cs
    public override string? DateDiff(DateDiffUnit unit)
        => unit switch
        {
            DateDiffUnit.Millisecond => "(TIMESTAMPDIFF(MICROSECOND, {0}, {1}) DIV 1000)",
            DateDiffUnit.Tick => "(TIMESTAMPDIFF(MICROSECOND, {0}, {1}) * 10)",
            DateDiffUnit.Nanosecond => "(TIMESTAMPDIFF(MICROSECOND, {0}, {1}) * 1000)",
            _ => "TIMESTAMPDIFF(" + unit.ToString().ToUpperInvariant() + ", {0}, {1})"
        };

    public override string? PadLeft => "CASE WHEN CHAR_LENGTH({0}) >= {1} THEN {0} ELSE LPAD({0}, {1}, {2}) END";
    public override string? PadRight => "CASE WHEN CHAR_LENGTH({0}) >= {1} THEN {0} ELSE RPAD({0}, {1}, {2}) END";

    // MySQL's TRIM(BOTH remstr FROM str) strips the literal substring remstr as one unit, not a set of characters in any
    // order: it has no character-set trim the way PostgreSQL's BTRIM or SQLite's and DuckDB's two-argument TRIM do.
    // REGEXP_REPLACE strips a maximal leading/trailing run of any character in a class instead, which is what
    // String.Trim(char[]) actually means. {1} (the escaped class content from TrimCharacterSet) is written twice in
    // TrimCharacters: once for each end, joined by CONCAT since MySQL has no string-literal concatenation operator.
    // Neither Pomelo nor Oracle's own provider gets this right: Pomelo declines to translate a set of more than one
    // character; Oracle's silently drops the characters and trims whitespace only.
    // MySQL before 8.0.4 has no REGEXP_REPLACE: a trim of a set is left untranslated there.
    public override string? TrimCharacters => features.RegexpFunctions ? "REGEXP_REPLACE({0}, CONCAT('^[', {1}, ']+|[', {1}, ']+$'), '')" : null;
    public override string? TrimStartCharacters => features.RegexpFunctions ? "REGEXP_REPLACE({0}, CONCAT('^[', {1}, ']+'), '')" : null;
    public override string? TrimEndCharacters => features.RegexpFunctions ? "REGEXP_REPLACE({0}, CONCAT('[', {1}, ']+$'), '')" : null;

    // Escaped for a regex character class: a literal ']' must not close the class early, '^' must not negate it, '-' must
    // not be read as a range, and '\' must not escape the following character.
    public override string TrimCharacterSet(char[] characters)
        => string.Concat(characters.Select(c => c is ']' or '^' or '\\' or '-' ? @"\" + c : c.ToString()));

    // Bitwise operators return a 64-bit unsigned integer (~12 is 18446744073709551603), so each result is read back as signed.
    // Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionVisitors/Internal/BitwiseOperationReturnTypeCorrectingExpressionVisitor.cs
    public override string? BitwiseAnd => "CAST({0} & {1} AS SIGNED)";
    public override string? BitwiseOr => "CAST({0} | {1} AS SIGNED)";
    public override string? BitwiseNot => "CAST(~{0} AS SIGNED)";
    public override string? ExclusiveOr => "CAST({0} ^ {1} AS SIGNED)";

    // The % operator, as the database's EF Core provider writes it.
    public override string? Modulo => null;

    public override string? Second => "EXTRACT(SECOND FROM {0})";
    public override string Concat => "CONCAT({0}, {1})";
    public override string? Floor => "(FLOOR({0}) + 0.0)";
    public override string? Ceiling => "(CEILING({0}) + 0.0)";

    // Shifts work on 64-bit unsigned integers, so each result is read back as signed, as the bitwise operators are.
    public override string? LeftShift => "CAST({0} << {1} AS SIGNED)";
    public override string? RightShift => "CAST({0} >> {1} AS SIGNED)";

    // Behaviour reference for the math, string and date and time functions below (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/Internal/MySqlMathMethodTranslator.cs
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/Internal/MySqlDateTimeMemberTranslator.cs
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/Internal/MySqlDateTimeMethodTranslator.cs
    public override string? Atan2 => "ATAN2({0}, {1})";
    public override string? Log2 => "LOG2({0})";
    public override string? Sign => "SIGN({0})";
    public override string? Truncate => "TRUNCATE({0}, 0)";
    public override string? Degrees => "DEGREES({0})";
    public override string? Radians => "RADIANS({0})";
    public override string? Greatest => "GREATEST({0}, {1})";
    public override string? Least => "LEAST({0}, {1})";

    // MySQL has no hyperbolic functions: their definitions in EXP and LN.
    public override string? Cosh => "((EXP({0}) + EXP(-({0}))) / 2)";
    public override string? Sinh => "((EXP({0}) - EXP(-({0}))) / 2)";
    public override string? Tanh => "((EXP({0}) - EXP(-({0}))) / (EXP({0}) + EXP(-({0}))))";
    public override string? Acosh => "LN({0} + SQRT({0} * {0} - 1))";
    public override string? Asinh => "LN({0} + SQRT({0} * {0} + 1))";
    public override string? Atanh => "(LN((1 + {0}) / (1 - {0})) / 2)";

    public override string? PositionFrom => "LOCATE({0}, {1}, {2})";

    // 'c': case-sensitive whatever the collation, as .NET's Regex. An empty pattern matches everything in .NET and is an error
    // in MySQL, so it is matched before REGEXP_LIKE sees it. MariaDB has no REGEXP_LIKE; its REGEXP is PCRE, where (?-i)
    // makes the match case-sensitive.
    // MySQL before 8.0.4 has no REGEXP_LIKE: its REGEXP is case-sensitive against a binary string.
    public override string? RegexIsMatch => mariaDb ? "CASE WHEN {1} = '' THEN TRUE ELSE {0} REGEXP CONCAT('(?-i)', {1}) END"
        : features.RegexpFunctions ? "CASE WHEN {1} = '' THEN TRUE ELSE REGEXP_LIKE({0}, {1}, 'c') END"
        : "CASE WHEN {1} = '' THEN TRUE ELSE {0} REGEXP BINARY {1} END";
    public override string? NewGuid => "UUID()";

    public override string? DayOfYear => "DAYOFYEAR({0})";
    public override string? DayOfWeek => "(DAYOFWEEK({0}) - 1)";
    public override string? Millisecond => "(EXTRACT(MICROSECOND FROM {0}) DIV 1000)";
    public override string? Microsecond => "(EXTRACT(MICROSECOND FROM {0}) % 1000)";

    // DATETIME(6) and TIME(6) keep microseconds.
    public override string? Nanosecond => "CASE WHEN {0} IS NOT NULL THEN 0 END";
    public override string? DateOf => "DATE({0})";
    public override string? TimeOfDay => "CAST({0} AS TIME(6))";
    public override string? Now => "CURRENT_TIMESTAMP(6)";
    public override string? UtcNow => "UTC_TIMESTAMP(6)";
    public override string? Today => "CURDATE()";
    public override string? AddYears => "DATE_ADD({0}, INTERVAL {1} YEAR)";
    public override string? AddMonths => "DATE_ADD({0}, INTERVAL {1} MONTH)";
    public override string? AddDays => "DATE_ADD({0}, INTERVAL {1} DAY)";
    public override string? AddMicroseconds => "DATE_ADD({0}, INTERVAL ROUND({1}) MICROSECOND)";

    // TO_DAYS counts from year 0, 366 days before 0001-01-01.
    public override string? DayNumber => "(TO_DAYS({0}) - 366)";
    public override string? DateFromDateTime => "DATE({0})";
    public override string? TimeFromDateTime => "CAST({0} AS TIME(6))";
    public override string? TimeFromTimeSpan => "{0}";
    public override string? DateAtTime => "TIMESTAMP({0}, {1})";
    public override string? AddTime => "ADDTIME({0}, {1})";
    public override string? TimeIsBetween => "CASE WHEN {1} <= {2} THEN {0} >= {1} AND {0} < {2} ELSE {0} >= {1} OR {0} < {2} END";

    // TIMEDIFF of two times may be negative; .NET's difference wraps past midnight.
    public override string? TimeDifference => "CASE WHEN {0} >= {1} THEN TIMEDIFF({0}, {1}) ELSE ADDTIME(TIMEDIFF({0}, {1}), '24:00:00') END";
    // TIME can't hold a multi-month DateTime difference, so subtracting two dates is left untranslated; TotalDays of the
    // difference has its own microsecond path.
    public override string? DateDifference => null;
    public override string? DateDifferenceTotalDays => "(TIMESTAMPDIFF(MICROSECOND, {1}, {0}) / 86400000000.0)";
    public override string? TimeSpanAdd => "ADDTIME({0}, {1})";
    public override string? TimeSpanSubtract => "SUBTIME({0}, {1})";

    // A DateTimeOffset is stored as its UTC instant.
    public override string? OffsetNow => "UTC_TIMESTAMP(6)";
    public override string? UnixSeconds => "TIMESTAMPDIFF(SECOND, '1970-01-01', {0})";
    public override string? UnixMilliseconds => "(TIMESTAMPDIFF(MICROSECOND, '1970-01-01', {0}) DIV 1000)";

    public override string? ByteLength => "LENGTH({0})";
    public override string? ByteAt => "ASCII(SUBSTRING({0}, {1}, 1))";

    // CHAR(n) is the one-byte binary string of byte n.
    public override string? BytesContain => "(LOCATE(CHAR({1}), {0}) > 0)";

    // A TIME counts hours past a day; a TimeSpan's Hours are those of its last day.
    public override string? TimeSpanHours => "(EXTRACT(HOUR FROM {0}) % 24)";
    public override string? TimeSpanMinutes => "EXTRACT(MINUTE FROM {0})";
    public override string? TimeSpanSeconds => "EXTRACT(SECOND FROM {0})";
    public override string? TimeSpanMilliseconds => "(EXTRACT(MICROSECOND FROM {0}) DIV 1000)";
    public override string? TimeSpanMicroseconds => "(EXTRACT(MICROSECOND FROM {0}) % 1000)";
    public override string? TimeSpanNanoseconds => "CASE WHEN {0} IS NOT NULL THEN 0 END";
}

internal sealed class MySqlTypeMappings : AnsiTypeMappings
{
    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "TINYINT(1)",
        [typeof(byte)] = "TINYINT UNSIGNED",
        // MySQL's own name, as its catalog reports it.
        [typeof(int)] = "INT",
        [typeof(float)] = "FLOAT",
        [typeof(double)] = "DOUBLE",
        [typeof(string)] = "LONGTEXT",
        [typeof(DateTime)] = "DATETIME(6)",
        [typeof(TimeOnly)] = "TIME(6)",
        [typeof(TimeSpan)] = "TIME(6)",
        [typeof(Guid)] = "CHAR(36)",
        // No type keeps an offset: the instant, in UTC, read back with offset zero, as both MySQL EF providers store it.
        // The driver writes a DateTimeOffset parameter in UTC and reads a DATETIME as one at offset zero.
        [typeof(DateTimeOffset)] = "DATETIME(6)",
        [typeof(byte[])] = "LONGBLOB"
    };

    private static readonly TimeSpan MaxTime = new(838, 59, 59);

    // MySQL's TIMESTAMP and DATETIME each hold a DateTime or a DateTimeOffset (its UTC instant), as both MySQL EF providers
    // map them; ISO's TIMESTAMP holds a DateTime only.
    private static readonly Type[] DateTimes = [typeof(DateTime), typeof(DateTimeOffset)];

    public override IReadOnlyList<Type> GetClrTypes(string storeTypeBase)
        => (string.Equals(storeTypeBase, "TIMESTAMP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(storeTypeBase, "DATETIME", StringComparison.OrdinalIgnoreCase))
            ? DateTimes
            : base.GetClrTypes(storeTypeBase);

    // One datetime type: bound as DbType.DateTime, as both MySQL EF providers bind it. Oracle's MySql.Data maps
    // DbType.DateTime2 to a BLOB and sends current-culture text (UPSTREAM_ISSUES.md); MySqlConnector takes either.
    public override System.Data.DbType DateTimeDbType
        => System.Data.DbType.DateTime;

    // TEXT and BLOB can't be keys or indexed without a prefix length. MySQL 8's 3072-byte InnoDB key limit leaves ample room
    // for the provider's default VARCHAR(255) key/index column at utf8mb4's four bytes per character.
    // Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlTypeMappingSource.cs
    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => facets switch
        {
            { KeyOrIndex: true, Size: null } when clrType == typeof(string) => "VARCHAR(255)",
            { KeyOrIndex: true, Size: null } when clrType == typeof(byte[]) => "VARBINARY(3072)",
            // CHAR and BINARY hold up to 255.
            { FixedLength: true, Size: > 0 and <= MaxFixedLength } when clrType == typeof(string) => $"CHAR({facets.Size})",
            { FixedLength: true, Size: > 0 and <= MaxFixedLength } when clrType == typeof(byte[]) => $"BINARY({facets.Size})",
            { Size: > 0 and <= MaxVarcharCharacters } when clrType == typeof(string) => $"VARCHAR({facets.Size})",
            { Size: > 0 and < MinBlobBytes } when clrType == typeof(byte[]) => $"VARBINARY({facets.Size})",
            // Every VARCHAR and VARBINARY byte counts toward a 65,535-byte row: a string longer than fits at utf8mb4's 4 bytes
            // a character is LONGTEXT, and binary of 8,000 bytes or more LONGBLOB. Behaviour reference (not copied): Pomelo's
            // MySqlTypeMappingSource and MySqlByteArrayTypeMapping (MIT, Pomelo Foundation):
            // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlTypeMappingSource.cs
            // A size of -1 is EF's "as large as the type allows".
            { Size: > MaxVarcharCharacters or < 0 } when clrType == typeof(string) => "LONGTEXT",
            { Size: >= MinBlobBytes or < 0 } when clrType == typeof(byte[]) => "LONGBLOB",
            _ => base.GetStoreType(clrType, facets)
        };

    private const int MaxFixedLength = 255;
    private const int MaxVarcharCharacters = 65_535 / 4;
    private const int MinBlobBytes = 8000;

    // TIME spans -838:59:59 to 838:59:59 and counts hours past a day; outside that MySQL would clip the value.
    public override string FormatLiteral(object value, string storeType, string literal)
    {
        // DATETIME keeps microseconds, and a literal (a column default) with more digits is refused, not rounded.
        if (value is DateTimeOffset offset)
        {
            return "TIMESTAMP '" + offset.UtcDateTime.ToString(@"yyyy\-MM\-dd HH\:mm\:ss.ffffff", CultureInfo.InvariantCulture) + "'";
        }

        if (value is DateTime dateTime)
        {
            return "TIMESTAMP '" + dateTime.ToString(@"yyyy\-MM\-dd HH\:mm\:ss.ffffff", CultureInfo.InvariantCulture) + "'";
        }

        if (value is not TimeSpan span)
        {
            return literal;
        }

        if (span.Duration() > MaxTime)
        {
            throw new ArgumentOutOfRangeException(nameof(value), span, $"MySQL's TIME holds -838:59:59 to 838:59:59; {span} is outside it.");
        }

        var magnitude = span.Duration();
        var fraction = (magnitude.Ticks % TimeSpan.TicksPerSecond) == 0 ? "" : magnitude.ToString(@"\.ffffff", CultureInfo.InvariantCulture);
        return "TIME '" + (span < TimeSpan.Zero ? "-" : "")
            + ((long)magnitude.TotalHours).ToString("00", CultureInfo.InvariantCulture)
            + magnitude.ToString(@"\:mm\:ss", CultureInfo.InvariantCulture) + fraction + "'";
    }

    // CAST takes MySQL's own targets: integers as SIGNED or UNSIGNED, text as CHAR, binary as BINARY (with a length where
    // the column has one); DECIMAL, DOUBLE, FLOAT, DATE, DATETIME, TIME and JSON as they are.
    public override string CastTarget(string storeType)
    {
        var open = storeType.IndexOf('(', StringComparison.Ordinal);
        var name = (open < 0 ? storeType : storeType[..open]).Trim().ToUpperInvariant();
        var length = open < 0 ? "" : storeType[open..storeType.IndexOf(')', open)] + ")";
        var unsigned = storeType.Contains("UNSIGNED", StringComparison.OrdinalIgnoreCase);
        return name.Replace(" UNSIGNED", "", StringComparison.Ordinal) switch
        {
            "TINYINT" or "SMALLINT" or "MEDIUMINT" or "INT" or "INTEGER" or "BIGINT" or "BIT" or "BOOL" or "BOOLEAN" or "YEAR"
                => unsigned ? "UNSIGNED" : "SIGNED",
            "CHAR" or "VARCHAR" => "CHAR" + length,
            "TINYTEXT" or "TEXT" or "MEDIUMTEXT" or "LONGTEXT" or "ENUM" or "SET" => "CHAR",
            "BINARY" or "VARBINARY" => "BINARY" + length,
            "TINYBLOB" or "BLOB" or "MEDIUMBLOB" or "LONGBLOB" => "BINARY",
            _ => storeType
        };
    }

    // CAST(x AS CHAR) has the connection's collation, and as strongly as a column's (IMPLICIT), so comparing it with a column of
    // another collation fails ("Illegal mix of collations": MariaDB 11's utf8mb4_uca1400_ai_ci beside MySqlConnector's
    // utf8mb4_general_ci). CONCAT(x) converts to text as weakly as a number does, so the other side's collation wins, as it
    // would for x itself; LEFT keeps a sized CHAR(n)'s truncation to n.
    public override string Cast(string storeType)
    {
        var target = CastTarget(storeType);
        if (target == "CHAR")
        {
            return "CONCAT({0})";
        }

        return target.StartsWith("CHAR(", StringComparison.Ordinal)
            ? "LEFT(CONCAT({0}), " + target["CHAR(".Length..^1] + ")"
            : base.Cast(storeType);
    }

    // A DateTime or byte[] row version is a TIMESTAMP(6), which the migrations give DEFAULT and ON UPDATE
    // CURRENT_TIMESTAMP(6); MySQL has no rowversion type, so a byte[] one reads the time as eight bytes. As Pomelo maps
    // both. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/BytesToDateTimeConverter.cs
    public override string? RowVersionStoreType(Type clrType)
        => clrType == typeof(DateTime) || clrType == typeof(byte[]) ? "timestamp(6)" : null;

    public override string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);
}

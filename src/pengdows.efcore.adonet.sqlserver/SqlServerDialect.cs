using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using pengdows.efcore.adonet.Metadata;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     SQL Server. Identifiers are bracketed, as the official provider writes them. BIT is not a
///     predicate, paging uses TOP or OFFSET/FETCH, and keys come from <c>SCOPE_IDENTITY()</c> so tables with triggers work.
/// </summary>
public sealed class SqlServerDialect : AdoNetDialect
{
    /// <summary>Compatibility level 160 (SQL Server 2022), as Microsoft's provider assumes.</summary>
    public static readonly SqlServerDialect Instance = new(160);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SqlServerDialect> Levels = new() { [160] = Instance };

    private SqlServerDialect(int compatibilityLevel)
    {
        CompatibilityLevel = compatibilityLevel;
        Sql = new SqlServerSql(compatibilityLevel);
    }

    /// <summary>
    ///     The dialect for a database at <paramref name="compatibilityLevel" /> (100 for SQL Server 2008 to 170): T-SQL a lower
    ///     level lacks is left untranslated, as Microsoft's provider does with UseCompatibilityLevel. One instance per level.
    ///     Behaviour reference (not copied):
    ///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/Translators/SqlServerStringMethodTranslator.cs
    /// </summary>
    public static SqlServerDialect ForCompatibilityLevel(int compatibilityLevel)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(compatibilityLevel, 100);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(compatibilityLevel, 170);
        return Levels.GetOrAdd(compatibilityLevel, level => new SqlServerDialect(level));
    }

    /// <summary>The database compatibility level whose T-SQL this dialect writes.</summary>
    public int CompatibilityLevel { get; }

    public override string Name => CompatibilityLevel == 160 ? "SQL Server" : $"SQL Server (compatibility level {CompatibilityLevel})";
    public override ISqlDialect Sql { get; }
    public override ITypeMappingDialect TypeMappings { get; } = new SqlServerTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new SqlServerCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 42,
        MaxParametersPerCommand = 2000,
        // The driver enlists in a TransactionScope. Behaviour reference (not copied):
        // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerConnection.cs
        SupportsAmbientTransactions = true
    };

    public override void ConfigureServices(IServiceCollection services)
    {
        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IProviderConventionSetBuilder, SqlServerConventionSetBuilder>();

        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, SqlServerMigrationsSqlGenerator>()
            .TryAdd<IRelationalQueryStringFactory, SqlServerQueryStringFactory>();
        services.TryAddScoped<IAdoNetBulkLoader, SqlServerBulkLoader>();
    }
}

internal sealed class SqlServerSql(int compatibilityLevel) : AnsiSqlDialect
{
    // Through master; a database still in use is put in single-user mode, rolling back other sessions, before it's dropped.
    // Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerDatabaseCreator.cs
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Server(
        "Initial Catalog",
        "master",
        dropSql: ["ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE", "DROP DATABASE {0}"]);

    public override string IdentifierQuotePrefix => "[";
    public override string IdentifierQuoteSuffix => "]";
    public override LateralJoinSyntax? LateralJoins => LateralJoinSyntax.Apply;
    public override string TrueLiteral => "CAST(1 AS bit)";
    public override string FalseLiteral => "CAST(0 AS bit)";
    public override string LongCountFunction => "COUNT_BIG";
    public override PagingSyntax Paging => PagingSyntax.TopOrOffsetFetch;
    public override BooleanSyntax Booleans => BooleanSyntax.BitValuesWithBitwiseOperators;
    public override bool RequiresDerivedTableColumnAliases => true;
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("SCOPE_IDENTITY()", "@@ROWCOUNT");
    public override DmlSyntax Dml => DmlSyntax.TargetAliasFrom;
    public override FunctionSyntax Functions { get; } = new SqlServerFunctions(compatibilityLevel);

    // The JSON functions and OPENJSON need compatibility level 130 (SQL Server 2016).
    public override JsonSyntax? Json => compatibilityLevel >= 130 ? JsonFunctions : null;

    private static readonly JsonSyntax JsonFunctions = new(
        "nvarchar(max)",
        "JSON_VALUE({0}, {1})",
        "JSON_QUERY({0}, {1})",
        "CONVERT({2}, JSON_VALUE({0}, {1}))",
        "CONVERT(bit, JSON_VALUE({0}, {1}))",
        binaryValue: "(SELECT [value] FROM OPENJSON({0}) WITH ([value] varbinary(max) {1}))",
        concatenate: "CONCAT({0}, {1})",
        set: "JSON_MODIFY({0}, {1}, {2})",
        fromText: "JSON_QUERY({0})",
        setBooleanValue: "CONVERT(bit, {0})")
    {
        StoresDocumentAsText = true,
        // OPENJSON's default value column is nvarchar(max); cast it in a derived table so EF's
        // shaper receives the element's declared store type while retaining the array ordinal.
        ArrayElements = "(SELECT CONVERT(int, [key]) AS [key], CONVERT({3}, [value]) AS [value] FROM OPENJSON({0}))"
    };
    public override SpatialSyntax Spatial { get; } = new()
    {
        Binary = SpatialBinary.Wkb,
        ParameterBinding = SpatialParameterBinding.UserDefinedType,
        FromText = "geometry::STGeomFromText({0}, {1})",
        StoreType = _ => "geometry",
        Functions = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Area"] = "{0}.STArea()",
            ["Boundary"] = "{0}.STBoundary()",
            ["Centroid"] = "{0}.STCentroid()",
            ["Dimension"] = "{0}.STDimension()",
            ["Envelope"] = "{0}.STEnvelope()",
            ["InteriorPoint"] = "{0}.STPointOnSurface()",
            ["PointOnSurface"] = "{0}.STPointOnSurface()",
            ["IsEmpty"] = "{0}.STIsEmpty()",
            ["IsSimple"] = "{0}.STIsSimple()",
            ["IsValid"] = "{0}.STIsValid()",
            ["Length"] = "{0}.STLength()",
            ["NumGeometries"] = "{0}.STNumGeometries()",
            ["NumPoints"] = "{0}.STNumPoints()",
            ["SRID"] = "{0}.STSrid",
            ["AsBinary"] = "{0}.STAsBinary()",
            ["AsText"] = "{0}.STAsText()",
            ["Buffer"] = "{0}.STBuffer({1})",
            ["Contains"] = "{0}.STContains({1})",
            ["ConvexHull"] = "{0}.STConvexHull()",
            ["Crosses"] = "{0}.STCrosses({1})",
            ["Difference"] = "{0}.STDifference({1})",
            ["Disjoint"] = "{0}.STDisjoint({1})",
            ["Distance"] = "{0}.STDistance({1})",
            ["EqualsTopologically"] = "{0}.STEquals({1})",
            ["Intersection"] = "{0}.STIntersection({1})",
            ["Intersects"] = "{0}.STIntersects({1})",
            ["IsWithinDistance"] = "CASE WHEN {0}.STDistance({1}) <= {2} THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
            ["Overlaps"] = "{0}.STOverlaps({1})",
            ["Relate"] = "{0}.STRelate({1}, {2})",
            ["SymmetricDifference"] = "{0}.STSymDifference({1})",
            ["Touches"] = "{0}.STTouches({1})",
            ["Union"] = "{0}.STUnion({1})",
            ["Within"] = "{0}.STWithin({1})",
            ["GetGeometryN"] = "CASE WHEN {0} IS NULL OR {1} IS NULL THEN NULL ELSE {0}.STGeometryN({1} + 1) END",
            ["X"] = "{0}.STX",
            ["Y"] = "{0}.STY",
            ["Z"] = null,
            ["M"] = null,
            ["StartPoint"] = "{0}.STStartPoint()",
            ["EndPoint"] = "{0}.STEndPoint()",
            ["IsClosed"] = "{0}.STIsClosed()",
            ["IsRing"] = "{0}.STIsRing()",
            ["GetPointN"] = "{0}.STPointN({1} + 1)",
            ["ExteriorRing"] = "{0}.STExteriorRing()",
            ["NumInteriorRings"] = "{0}.STNumInteriorRing()",
            ["GetInteriorRingN"] = "{0}.STInteriorRingN({1} + 1)",
            ["GeometryType"] = "{0}.STGeometryType()",
            ["OgcGeometryType"] = "CASE {0}.STGeometryType() WHEN 'Point' THEN 1 WHEN 'LineString' THEN 2 WHEN 'Polygon' THEN 3 WHEN 'MultiPoint' THEN 4 WHEN 'MultiLineString' THEN 5 WHEN 'MultiPolygon' THEN 6 WHEN 'GeometryCollection' THEN 7 END"
        }
    };
    // An explicit value for an IDENTITY column needs IDENTITY_INSERT, which one table per session may have on. Behaviour
    // reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Migrations/SqlServerMigrationsSqlGenerator.cs
    public override IdentityInsertSyntax IdentityInsert { get; } = new("SET IDENTITY_INSERT {0} ON", "SET IDENTITY_INSERT {0} OFF");

    public override bool PreferExistsToIn => true;
    public override string CurrentSchema => "SCHEMA_NAME()";

    // T-SQL has no row value comparisons.
    public override bool RowValues => false;
    public override bool UsesNativeValuesTable => true;

    // SqlClient's SqlTransaction has Save and Rollback(name) but reports no savepoints; they last until the transaction ends.
    public override SavepointSyntax Savepoints { get; } = new("SAVE TRANSACTION {0}", "ROLLBACK TRANSACTION {0}", release: null);

    public override string StringLiteral(string value)
        => "N" + base.StringLiteral(value);
}

/// <summary>
///     Mostly as the official provider writes them. LEN ignores trailing spaces, so lengths count after spaces are replaced
///     (one pass over the string; the official provider writes plain LEN and gets them wrong), and a substring to the end
///     is bounded by DATALENGTH, which is never less than the length. LEN returns bigint for MAX types, hence the CAST.
///     CHARINDEX finds an empty string nowhere; .NET finds it at 0.
/// </summary>
internal sealed class SqlServerFunctions(int compatibilityLevel) : FunctionSyntax
{
    // TRIM needs compatibility level 140 (SQL Server 2017), TRIM(LEADING ...) and LTRIM/RTRIM(string, characters) 160 (2022).
    public override string? Trim => compatibilityLevel >= 140 ? "TRIM({0})" : "LTRIM(RTRIM({0}))";
    public override string? TrimCharacter => compatibilityLevel >= 160 ? base.TrimCharacter : compatibilityLevel >= 140 ? "TRIM({1} FROM {0})" : null;
    public override string? TrimStartCharacter => compatibilityLevel >= 160 ? base.TrimStartCharacter : null;
    public override string? TrimEndCharacter => compatibilityLevel >= 160 ? base.TrimEndCharacter : null;

    // T-SQL's bitwise operators.
    public override string? BitwiseAnd => "{0} & {1}";
    public override string? BitwiseOr => "{0} | {1}";
    public override string? BitwiseNot => "~{0}";
    public override string? ExclusiveOr => "{0} ^ {1}";

    // The % operator, as the database's EF Core provider writes it.
    public override string? Modulo => null;

    public override string Concat => "{0} + {1}";
    public override string? TrimStart => "LTRIM({0})";
    public override string? TrimEnd => "RTRIM({0})";

    // TRIM(characters FROM string) (SQL Server 2017) and LTRIM/RTRIM(string, characters) (SQL Server 2022) remove any of
    // the characters. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/Translators/SqlServerStringMethodTranslator.cs
    public override string? TrimCharacters => compatibilityLevel >= 140 ? "TRIM({1} FROM {0})" : null;

    // No LPAD/RPAD: the pad characters joined to the string and cut to the width; LEN ignores trailing spaces, so the length
    // is LEN of the string with a character after it, less one.
    public override string? PadLeft
        => "CASE WHEN LEN({0} + N'x') - 1 >= {1} THEN {0} ELSE RIGHT(REPLICATE({2}, {1}) + {0}, {1}) END";

    public override string? PadRight
        => "CASE WHEN LEN({0} + N'x') - 1 >= {1} THEN {0} ELSE LEFT({0} + REPLICATE({2}, {1}), {1}) END";
    public override string? TrimStartCharacters => compatibilityLevel >= 160 ? "LTRIM({0}, {1})" : null;
    public override string? TrimEndCharacters => compatibilityLevel >= 160 ? "RTRIM({0}, {1})" : null;
    public override string? Length => "CAST(LEN(REPLACE({0}, ' ', '_')) AS int)";
    public override string? Substring => "SUBSTRING({0}, {1}, {2})";
    public override string? SubstringFrom => "SUBSTRING({0}, {1}, DATALENGTH({0}))";
    // CHARINDEX('', x) is 0, where an empty pattern is at position 1; a null string stays null.
    public override string? Position => "CAST(CASE WHEN {1} IS NULL THEN NULL WHEN DATALENGTH({0}) = 0 THEN 1 ELSE CHARINDEX({0}, {1}) END AS int)";
    public override string? Round => "ROUND({0}, 0)";
    public override string? Year => "DATEPART(year, {0})";
    public override string? Month => "DATEPART(month, {0})";
    public override string? Day => "DATEPART(day, {0})";
    public override string? Hour => "DATEPART(hour, {0})";
    public override string? Minute => "DATEPART(minute, {0})";
    public override string? Second => "DATEPART(second, {0})";
    public override string? Millisecond => "DATEPART(millisecond, {0})";
    public override string? DayOfYear => "DATEPART(dayofyear, {0})";
    public override string? Microsecond => "DATEPART(microsecond, {0}) % 1000";
    public override string? Nanosecond => "DATEPART(nanosecond, {0}) % 1000";
    public override string? DateOf => "CONVERT(date, {0})";
    public override string? TimeOfDay => "CONVERT(time, {0})";
    public override string? Now => "SYSDATETIME()";
    // SYSDATETIME/SYSUTCDATETIME are datetime2, keeping the precision a DateTime maps to; GETDATE/GETUTCDATE are datetime.
    public override string? UtcNow => "SYSUTCDATETIME()";
    public override string? Today => "CONVERT(date, GETDATE())";
    public override string? AddYears => "DATEADD(year, {1}, {0})";
    public override string LikeSpecialCharacters => "%_[";
}

internal sealed class SqlServerTypeMappings : AnsiTypeMappings
{
    // As the official provider names them.
    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "bit",
        [typeof(byte)] = "tinyint",
        [typeof(short)] = "smallint",
        [typeof(int)] = "int",
        [typeof(long)] = "bigint",
        [typeof(float)] = "real",
        [typeof(double)] = "float",
        [typeof(decimal)] = "decimal(18,2)",
        [typeof(string)] = "nvarchar(max)",
        [typeof(DateTime)] = "datetime2",
        [typeof(DateTimeOffset)] = "datetimeoffset",
        [typeof(DateOnly)] = "date",
        [typeof(TimeOnly)] = "time",
        [typeof(TimeSpan)] = "time",
        [typeof(Guid)] = "uniqueidentifier",
        [typeof(byte[])] = "varbinary(max)"
    };

    // SQL Server's own names; "timestamp" is rowversion here, not ISO's date and time.
    private static readonly Dictionary<string, Type[]> ClrTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bit"] = [typeof(bool)],
        ["tinyint"] = [typeof(byte)],
        ["money"] = [typeof(decimal)],
        ["smallmoney"] = [typeof(decimal)],
        ["datetime"] = [typeof(DateTime)],
        ["datetime2"] = [typeof(DateTime)],
        ["smalldatetime"] = [typeof(DateTime)],
        ["datetimeoffset"] = [typeof(DateTimeOffset)],
        ["uniqueidentifier"] = [typeof(Guid)],
        ["ntext"] = [typeof(string)],
        ["xml"] = [typeof(string)],
        ["image"] = [typeof(byte[])],
        ["rowversion"] = [typeof(byte[])],
        ["timestamp"] = [typeof(byte[])]
    };

    // rowversion: eight bytes the database sets on every insert and update. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerTypeMappingSource.cs
    public override string? RowVersionStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "rowversion" : null;

    public override string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);

    // Literals as the official provider writes them (its SqlServer*TypeMapping classes, MIT, .NET Foundation).
    public override string FormatLiteral(object value, string storeType, string literal)
        => value switch
        {
            short or byte or long or float => $"CAST({literal} AS {storeType})",
            double d when double.IsSubnormal(d) => SubnormalLiteral(d),
            double => FloatLiteral(literal),
            DateTime dateTime => Format(dateTime, BaseName(storeType) switch
            {
                "date" => "yyyy-MM-dd",
                "datetime" => "yyyy-MM-ddTHH:mm:ss.fff",
                "smalldatetime" => "yyyy-MM-ddTHH:mm:ss",
                _ => "yyyy-MM-ddTHH:mm:ss" + Fraction('f', Precision(storeType)) + "K"
            }),
            DateOnly date => Format(date, "yyyy-MM-dd"),
            TimeOnly time => Format(time, time.Ticks % TimeSpan.TicksPerSecond == 0 ? @"HH\:mm\:ss" : @"HH\:mm\:ss" + Fraction('F', Precision(storeType), @"\.")),
            TimeSpan span => TimeLiteral(span, storeType),
            DateTimeOffset offset => Format(offset, "yyyy-MM-ddTHH:mm:ss" + Fraction('f', Precision(storeType)) + "zzz"),
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            _ => literal
        };

    // time holds a time of day: a span outside [0, 24h) has no time literal, and writing its hours alone would change it.
    // A float literal has an exponent, so it isn't read as a decimal.
    private static string FloatLiteral(string literal)
        => literal.Contains('E', StringComparison.OrdinalIgnoreCase) || !char.IsDigit(literal[^1]) ? literal : literal + "E0";

    // SQL Server reads a literal below 2.2250738585072014E-308 as 0, though its arithmetic keeps such values: the value is
    // written times 2^52, which is exact and normal, and divided back. The official provider writes the plain literal (see
    // UPSTREAM_ISSUES.md). Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerDoubleTypeMapping.cs
    private static string SubnormalLiteral(double value)
        => $"({FloatLiteral((value * 4503599627370496d).ToString("R", CultureInfo.InvariantCulture))} / 4503599627370496E0)";

    private static string TimeLiteral(TimeSpan span, string storeType)
    {
        if ((span < TimeSpan.Zero) || (span >= TimeSpan.FromDays(1)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(span), span, $"A '{storeType}' column holds a time of day, from 00:00:00 up to 24 hours; {span} isn't one.");
        }

        return Format(span, (span.Ticks % TimeSpan.TicksPerSecond) == 0 ? @"hh\:mm\:ss" : @"hh\:mm\:ss" + Fraction('F', Precision(storeType), @"\."));
    }

    private static string BaseName(string storeType)
        => storeType.IndexOf('(') is > 0 and var parenthesis ? storeType[..parenthesis].Trim() : storeType;

    private static int Precision(string storeType)
        => storeType.IndexOf('(') is > 0 and var open && int.TryParse(storeType.AsSpan(open + 1, storeType.Length - open - 2), out var precision)
            && precision is >= 0 and <= 7
                ? precision
                : 7;

    private static string Fraction(char digit, int precision, string separator = ".")
        => precision == 0 ? "" : separator + new string(digit, precision);

    private static string Format(IFormattable value, string format)
        => "'" + value.ToString(format, CultureInfo.InvariantCulture) + "'";

    // Bounded sizes keep one plan for values of any length up to them.
    public override int? StringParameterSize(bool unicode)
        => unicode ? 4000 : 8000;

    public override IReadOnlyList<Type> GetClrTypes(string storeTypeBase)
        => ClrTypes.TryGetValue(storeTypeBase, out var types) ? types : base.GetClrTypes(storeTypeBase);

    // nvarchar and nchar hold up to 4000 characters, varchar, char, varbinary and binary up to 8000 bytes; beyond, (max). An
    // index key holds 900 bytes, so an unsized key or index column is nvarchar(450), varchar(900) or varbinary(900).
    // Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerTypeMappingSource.cs
    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => clrType == typeof(string) ? StringType(facets)
            : (clrType == typeof(byte[])) && ((facets.Size ?? (facets.KeyOrIndex ? 900 : null)) is { } size)
                ? (size <= 8000 ? (facets.FixedLength ? $"binary({size})" : $"varbinary({size})") : "varbinary(max)")
            : (clrType == typeof(decimal)) && (facets.Precision is { } precision) ? $"decimal({precision},{facets.Scale ?? 0})"
            : base.GetStoreType(clrType, facets);

    private static string? StringType(StoreTypeFacets facets)
    {
        var (prefix, bound, keySize) = facets.Unicode ? ("n", 4000, 450) : ("", 8000, 900);
        if ((facets.Size ?? (facets.KeyOrIndex ? keySize : null)) is not { } size)
        {
            return facets.Unicode ? null : "varchar(max)";
        }

        return size <= bound ? $"{prefix}{(facets.FixedLength ? "char" : "varchar")}({size})" : $"{prefix}varchar(max)";
    }
}

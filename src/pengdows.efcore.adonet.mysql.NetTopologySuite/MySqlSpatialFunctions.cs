using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.NetTopologySuite;

namespace pengdows.efcore.adonet.mysql.NetTopologySuite;

/// <summary>How <see cref="MySqlSpatialDbFunctionsExtensions.SpatialDistanceSphere" /> measures, as Pomelo's.</summary>
public enum SpatialDistanceAlgorithm
{
    /// <summary>The server's own ST_Distance_Sphere.</summary>
    Native,

    /// <summary>Andoyer's formula on the WGS 84 ellipsoid: MySQL's ST_Distance on SRID 4326, written out on MariaDB.</summary>
    Andoyer,

    /// <summary>The haversine formula, on a sphere.</summary>
    Haversine
}

/// <summary>
///     MySQL's spatial distances in queries, as Pomelo's MySqlSpatialDbFunctionsExtensions. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.NTS/Extensions/MySqlSpatialDbFunctionsExtensions.cs
/// </summary>
public static class MySqlSpatialDbFunctionsExtensions
{
    /// <summary>
    ///     The distance between <paramref name="g1" /> and <paramref name="g2" /> in their spatial reference system's units, on
    ///     the plane: MySQL 8's ST_Distance with the SRID set to 0 (it measures a geographic one on the ellipsoid), MariaDB's
    ///     ST_Distance whatever the SRID.
    /// </summary>
    public static double SpatialDistancePlanar(this DbFunctions _, Geometry g1, Geometry g2)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SpatialDistancePlanar)));

    /// <summary>
    ///     The least distance, in meters, between the points of <paramref name="g1" /> and <paramref name="g2" /> (SRID 4326) on
    ///     the earth, by <paramref name="algorithm" />, which must be a constant.
    /// </summary>
    public static double SpatialDistanceSphere(this DbFunctions _, Geometry g1, Geometry g2, SpatialDistanceAlgorithm algorithm)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SpatialDistanceSphere)));
}

/// <summary>Turns on MySQL's NetTopologySuite types and spatial functions.</summary>
public static class MySqlNetTopologySuiteExtensions
{
    /// <summary>
    ///     NetTopologySuite's geometry types (the shared <c>UseNetTopologySuite</c>) and MySQL's spatial distance functions,
    ///     <see cref="MySqlSpatialDbFunctionsExtensions" />: what Pomelo's <c>UseNetTopologySuite</c> turns on.
    /// </summary>
    [RequiresDynamicCode("NetTopologySuite's types aren't supported with NativeAOT.")]
    public static AdoNetDbContextOptionsBuilder UseMySqlNetTopologySuite(this AdoNetDbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNetTopologySuite();
        var builder = ((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder;
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
            builder.Options.FindExtension<MySqlNetTopologySuiteOptionsExtension>() ?? new MySqlNetTopologySuiteOptionsExtension());
        return optionsBuilder;
    }
}

internal sealed class MySqlNetTopologySuiteOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMethodCallTranslatorPlugin, MySqlSpatialTranslatorPlugin>()
            .TryAdd<IEvaluatableExpressionFilterPlugin, MySqlSpatialEvaluatableExpressionFilterPlugin>();

    public void Validate(IDbContextOptions options)
    {
        if (options.FindExtension<AdoNetOptionsExtension>()?.Dialect is not MySqlDialect)
        {
            throw new InvalidOperationException("UseMySqlNetTopologySuite needs a MySQL dialect (UseAdoNetMySql).");
        }
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider
            => false;

        public override string LogFragment
            => "using MySQL's spatial functions ";

        public override int GetServiceProviderHashCode()
            => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["MySqlNetTopologySuite"] = "1";
    }
}

/// <summary>Keeps the spatial functions in the query even when both geometries are constants.</summary>
internal sealed class MySqlSpatialEvaluatableExpressionFilterPlugin : IEvaluatableExpressionFilterPlugin
{
    public bool IsEvaluatableExpression(Expression expression)
        => (expression is not MethodCallExpression call) || (call.Method.DeclaringType != typeof(MySqlSpatialDbFunctionsExtensions));
}

internal sealed class MySqlSpatialTranslatorPlugin(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect dialect) : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } =
        [new MySqlSpatialTranslator(
            sqlExpressionFactory,
            typeMappingSource,
            dialect is MySqlSql sql ? sql.Features : MySqlServerFeatures.For(mariaDb: false, version: null))];
}

/// <summary>
///     SpatialDistancePlanar and SpatialDistanceSphere. ST_Distance is null for an empty geometry, so these are not null only
///     when their arguments aren't. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.NTS/Query/ExpressionTranslators/Internal/MySqlDbFunctionsExtensionsMethodTranslator.cs
/// </summary>
internal sealed class MySqlSpatialTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    MySqlServerFeatures features)
    : IMethodCallTranslator
{
    // Pomelo's sphere for the haversine (PostGIS's mean radius); WGS 84 for Andoyer's formula.
    private const string SphereRadius = "6370986.0";
    private const string EquatorialRadius = "6378137.0";
    private const string Flattening = "(1.0 / 298.257223563)";

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(MySqlSpatialDbFunctionsExtensions))
        {
            return null;
        }

        var mapping = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions.InferTypeMapping(arguments[1], arguments[2]);
        SqlExpression[] geometries = [sqlExpressionFactory.ApplyTypeMapping(arguments[1], mapping)!, sqlExpressionFactory.ApplyTypeMapping(arguments[2], mapping)!];
        var template = method.Name == nameof(MySqlSpatialDbFunctionsExtensions.SpatialDistancePlanar)
            ? Planar()
            : arguments[3] is SqlConstantExpression { Value: SpatialDistanceAlgorithm algorithm } ? Sphere(algorithm) : null;
        return template == null
            ? null
            : sqlExpressionFactory.CallMaybeNull(template, geometries, typeof(double), typeMappingSource.FindMapping(typeof(double)));
    }

    // MySQL 8 measures a geographic SRID on the ellipsoid: planar is with the SRID set to 0. MariaDB, and MySQL before 8.0,
    // are always planar.
    private string Planar()
        => features.GeographicDistance
            ? "CASE WHEN ST_SRID({0}) = 0 THEN ST_Distance({0}, {1}) ELSE ST_Distance(ST_SRID({0}, 0), ST_SRID({1}, 0)) END"
            : "ST_Distance({0}, {1})";

    private string Sphere(SpatialDistanceAlgorithm algorithm)
        => algorithm switch
        {
            // Where the server has no ST_Distance_Sphere (MySQL before 8.0 by Pomelo's flags, MariaDB before 10.5.10 and its
            // backports), Andoyer's formula, as Pomelo falls back to.
            SpatialDistanceAlgorithm.Native => features.DistanceSphere ? "ST_Distance_Sphere({0}, {1})" : Sphere(SpatialDistanceAlgorithm.Andoyer),

            // MySQL 8's ST_Distance is Andoyer's on SRID 4326; MariaDB, and MySQL before 8.0, have none, so the formula is
            // written out.
            SpatialDistanceAlgorithm.Andoyer => features.GeographicDistance
                ? "CASE WHEN ST_SRID({0}) = 4326 THEN ST_Distance({0}, {1}) ELSE ST_Distance(ST_SRID({0}, 4326), ST_SRID({1}, 4326)) END"
                : Andoyer("{0}", "{1}"),

            // The coordinates as written, which MySQL 8 reads as latitude first on a geographic SRID: set it to 0 first.
            _ => features.GeographicDistance
                ? "CASE WHEN ST_SRID({0}) = 0 THEN " + Haversine("{0}", "{1}") + " ELSE " + Haversine("ST_SRID({0}, 0)", "ST_SRID({1}, 0)") + " END"
                : Haversine("{0}", "{1}")
        };

    private static string Radians(string coordinate)
        => "(" + coordinate + " * PI() / 180)";

    // The great-circle distance on a sphere: 2r asin(sqrt(sin²(Δφ/2) + cos φ1 cos φ2 sin²(Δλ/2))).
    private static string Haversine(string p1, string p2)
        => "(" + SphereRadius + " * 2 * ASIN(SQRT("
            + "POWER(SIN((" + Radians("ST_Y(" + p2 + ")") + " - " + Radians("ST_Y(" + p1 + ")") + ") / 2), 2) + "
            + "COS(" + Radians("ST_Y(" + p1 + ")") + ") * COS(" + Radians("ST_Y(" + p2 + ")") + ") * "
            + "POWER(SIN((" + Radians("ST_X(" + p2 + ")") + " - " + Radians("ST_X(" + p1 + ")") + ") / 2), 2))))";

    // Andoyer's approximation of the geodesic on an ellipsoid of equatorial radius a and flattening fl:
    // g = (φ1 - φ2)/2, λ = (λ1 - λ2)/2, f = (φ1 + φ2)/2; s = sin²g cos²λ + cos²f sin²λ; c = cos²g cos²λ + sin²f sin²λ;
    // ω = atan(sqrt(s/c)); r = sqrt(s c)/ω; d = 2ωa; h1 = (3r - 1)/(2c); h2 = (3r + 1)/(2s);
    // distance = d (1 + fl (h1 sin²f cos²g - h2 cos²f sin²g)). The same point (s = 0) is 0.
    private static string Andoyer(string p1, string p2)
    {
        var g = "((" + Radians("ST_Y(" + p1 + ")") + " - " + Radians("ST_Y(" + p2 + ")") + ") / 2)";
        var l = "((" + Radians("ST_X(" + p1 + ")") + " - " + Radians("ST_X(" + p2 + ")") + ") / 2)";
        var f = "((" + Radians("ST_Y(" + p1 + ")") + " + " + Radians("ST_Y(" + p2 + ")") + ") / 2)";
        string Squared(string function, string angle)
            => "POWER(" + function + "(" + angle + "), 2)";
        var s = "(" + Squared("SIN", g) + " * " + Squared("COS", l) + " + " + Squared("COS", f) + " * " + Squared("SIN", l) + ")";
        var c = "(" + Squared("COS", g) + " * " + Squared("COS", l) + " + " + Squared("SIN", f) + " * " + Squared("SIN", l) + ")";
        var omega = "ATAN(SQRT(" + s + " / " + c + "))";
        var r = "(SQRT(" + s + " * " + c + ") / " + omega + ")";
        var h1 = "((3 * " + r + " - 1) / (2 * " + c + "))";
        var h2 = "((3 * " + r + " + 1) / (2 * " + s + "))";
        return "CASE WHEN " + s + " = 0 THEN 0.0 ELSE (2 * " + omega + " * " + EquatorialRadius + ") * (1 + " + Flattening + " * ("
            + h1 + " * " + Squared("SIN", f) + " * " + Squared("COS", g) + " - " + h2 + " * " + Squared("COS", f) + " * " + Squared("SIN", g)
            + ")) END";
    }
}

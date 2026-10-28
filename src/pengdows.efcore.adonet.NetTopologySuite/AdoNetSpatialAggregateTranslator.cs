using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Union;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.NetTopologySuite;

/// <summary>
///     NetTopologySuite's aggregates over a group of geometries, each written through the dialect's <see cref="SpatialSyntax" />
///     template of its name, <c>{0}</c> the group's geometries: <c>GeometryCombiner.Combine</c>, <c>ConvexHull.Create</c>,
///     <c>UnaryUnionOp.Union</c> and <c>EnvelopeCombiner.CombineAsGeometry</c>. A dialect with no template leaves one
///     untranslated. A filter in the group is a CASE, a Distinct is DISTINCT. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Query/Internal/SqliteNetTopologySuiteAggregateMethodTranslator.cs
/// </summary>
internal sealed class AdoNetSpatialAggregateTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect sql) : IAggregateMethodCallTranslator
{
    private static readonly Dictionary<MethodInfo, string> Aggregates = new()
    {
        [typeof(GeometryCombiner).GetRuntimeMethod(nameof(GeometryCombiner.Combine), [typeof(IEnumerable<Geometry>)])!] = "GeometryCombiner.Combine",
        [typeof(ConvexHull).GetRuntimeMethod(nameof(ConvexHull.Create), [typeof(IEnumerable<Geometry>)])!] = "ConvexHull.Create",
        [typeof(UnaryUnionOp).GetRuntimeMethod(nameof(UnaryUnionOp.Union), [typeof(IEnumerable<Geometry>)])!] = "UnaryUnionOp.Union",
        [typeof(EnvelopeCombiner).GetRuntimeMethod(nameof(EnvelopeCombiner.CombineAsGeometry), [typeof(IEnumerable<Geometry>)])!]
            = "EnvelopeCombiner.CombineAsGeometry"
    };

    public SqlExpression? Translate(
        MethodInfo method,
        EnumerableExpression source,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((source.Selector is not SqlExpression geometries)
            || !Aggregates.TryGetValue(method, out var name)
            || (sql.Spatial?.Function(name) is not { } template))
        {
            return null;
        }

        if (source.Predicate != null)
        {
            geometries = sqlExpressionFactory.Case([new CaseWhenClause(source.Predicate, geometries)], elseResult: null);
        }

        if (source.IsDistinct)
        {
            geometries = new DistinctExpression(geometries);
        }

        return sqlExpressionFactory.Call(template, [geometries], typeof(Geometry), typeMappingSource.FindMapping(typeof(Geometry)));
    }
}

/// <summary>Registers <see cref="AdoNetSpatialAggregateTranslator" />.</summary>
internal sealed class AdoNetSpatialAggregateMethodCallTranslatorPlugin(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect sql) : IAggregateMethodCallTranslatorPlugin
{
    public IEnumerable<IAggregateMethodCallTranslator> Translators { get; } =
        [new AdoNetSpatialAggregateTranslator(sqlExpressionFactory, typeMappingSource, sql)];
}

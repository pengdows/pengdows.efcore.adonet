using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Metadata;

namespace pengdows.efcore.adonet.sqlite;

/// <summary>
///     SpatiaLite's columns and functions, for the NetTopologySuite package (<c>UseNetTopologySuite()</c>): values are its GAIA
///     blob, constants GeomFromText, and members its functions, which mostly lack ISO's ST_ prefix. Behaviour reference (not
///     copied): https://github.com/dotnet/efcore/tree/v10.0.11/src/EFCore.Sqlite.NTS/Query/Internal
/// </summary>
internal static class SqliteSpatial
{
    // SpatiaLite's GeometryType is upper case, with a coordinate suffix after a space: 'POINT', 'POINT Z', 'POINT ZM'.
    private const string GeometryType = "RTRIM(GeometryType({0}), ' ZM')";

    public static SpatialSyntax Syntax { get; } = new()
    {
        Binary = SpatialBinary.Gaia,
        FromText = "GeomFromText({0}, {1})",
        Functions = new Dictionary<string, string?>
        {
            ["Area"] = "Area({0})",
            ["Boundary"] = "Boundary({0})",
            ["Centroid"] = "Centroid({0})",
            ["Dimension"] = "Dimension({0})",
            ["Envelope"] = "Envelope({0})",
            ["InteriorPoint"] = "PointOnSurface({0})",
            ["PointOnSurface"] = "PointOnSurface({0})",
            ["IsEmpty"] = "IsEmpty({0})",
            ["IsSimple"] = "IsSimple({0})",
            ["IsValid"] = "IsValid({0})",
            ["Length"] = "GLength({0})",
            ["NumGeometries"] = "NumGeometries({0})",
            ["NumPoints"] = "NumPoints({0})",
            ["SRID"] = "SRID({0})",
            ["AsBinary"] = "AsBinary({0})",
            ["AsText"] = "AsText({0})",
            ["Buffer"] = "Buffer({0}, {1})",
            ["Contains"] = "Contains({0}, {1})",
            ["ConvexHull"] = "ConvexHull({0})",
            ["CoveredBy"] = "CoveredBy({0}, {1})",
            ["Covers"] = "Covers({0}, {1})",
            ["Crosses"] = "Crosses({0}, {1})",
            ["Difference"] = "Difference({0}, {1})",
            ["Disjoint"] = "Disjoint({0}, {1})",
            ["Distance"] = "Distance({0}, {1})",
            ["EqualsTopologically"] = "Equals({0}, {1})",
            ["Intersection"] = "Intersection({0}, {1})",
            ["Intersects"] = "Intersects({0}, {1})",
            ["IsWithinDistance"] = "(Distance({0}, {1}) <= {2})",
            ["Overlaps"] = "Overlaps({0}, {1})",
            ["Relate"] = "Relate({0}, {1}, {2})",
            ["Reverse"] = "ST_Reverse({0})",
            ["SymmetricDifference"] = "SymDifference({0}, {1})",
            ["Touches"] = "Touches({0}, {1})",
            ["Union"] = "GUnion({0}, {1})",
            ["UnaryUnion"] = "UnaryUnion({0})",
            ["Within"] = "Within({0}, {1})",
            ["GetGeometryN"] = "GeometryN({0}, {1} + 1)",
            ["X"] = "X({0})",
            ["Y"] = "Y({0})",
            ["Z"] = "Z({0})",
            ["M"] = "M({0})",
            ["StartPoint"] = "StartPoint({0})",
            ["EndPoint"] = "EndPoint({0})",
            ["IsClosed"] = "IsClosed({0})",
            ["IsRing"] = "IsRing({0})",
            ["GetPointN"] = "PointN({0}, {1} + 1)",
            ["ExteriorRing"] = "ExteriorRing({0})",
            ["NumInteriorRings"] = "NumInteriorRing({0})",
            ["GetInteriorRingN"] = "InteriorRingN({0}, {1} + 1)",
            ["GeometryType"] = "CASE " + GeometryType + " WHEN 'POINT' THEN 'Point' WHEN 'LINESTRING' THEN 'LineString' "
                + "WHEN 'POLYGON' THEN 'Polygon' WHEN 'MULTIPOINT' THEN 'MultiPoint' WHEN 'MULTILINESTRING' THEN 'MultiLineString' "
                + "WHEN 'MULTIPOLYGON' THEN 'MultiPolygon' WHEN 'GEOMETRYCOLLECTION' THEN 'GeometryCollection' END",
            ["OgcGeometryType"] = "CASE " + GeometryType + " WHEN 'POINT' THEN 1 WHEN 'LINESTRING' THEN 2 WHEN 'POLYGON' THEN 3 "
                + "WHEN 'MULTIPOINT' THEN 4 WHEN 'MULTILINESTRING' THEN 5 WHEN 'MULTIPOLYGON' THEN 6 WHEN 'GEOMETRYCOLLECTION' THEN 7 END"
        }
    };
}

/// <summary>
///     Carries <see cref="SqliteModelOptionsExtensions.HasSrid(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder, int)" />
///     to the columns migrations see, and asks for SpatiaLite's catalog when a table has a geometry column. Behaviour reference
///     (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Metadata/Internal/SqliteAnnotationProvider.cs
/// </summary>
internal sealed class SqliteAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : AdoNetAnnotationProvider(dependencies)
{
    internal const string InitSpatialMetaData = "AdoNet:Sqlite:InitSpatialMetaData";

    private static readonly HashSet<string> GeometryKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "GEOMETRY", "POINT", "LINESTRING", "POLYGON", "MULTIPOINT", "MULTILINESTRING", "MULTIPOLYGON", "GEOMETRYCOLLECTION"
    };

    /// <summary>A SpatiaLite geometry type, with or without its Z, M or ZM suffix.</summary>
    public static bool IsGeometryType(string? storeType)
        => (storeType != null)
            && (GeometryKinds.Contains(storeType)
                || (storeType.EndsWith("ZM", StringComparison.OrdinalIgnoreCase) && GeometryKinds.Contains(storeType[..^2]))
                || ((storeType.EndsWith('Z') || storeType.EndsWith('z') || storeType.EndsWith('M') || storeType.EndsWith('m'))
                    && GeometryKinds.Contains(storeType[..^1])));

    public override IEnumerable<IAnnotation> For(IRelationalModel model, bool designTime)
    {
        foreach (var annotation in base.For(model, designTime))
        {
            yield return annotation;
        }

        if (model.Tables.SelectMany(t => t.Columns).Any(c => IsGeometryType(c.StoreType)))
        {
            yield return new Annotation(InitSpatialMetaData, true);
        }
    }

    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        if (column.PropertyMappings.Select(m => m.Property.FindAnnotation(SqliteModelOptionsExtensions.SridAnnotation)?.Value).OfType<int>().FirstOrDefault() is var srid
            && (srid != 0))
        {
            yield return new Annotation(SqliteModelOptionsExtensions.SridAnnotation, srid);
        }
    }
}

/// <summary>
///     Loads SpatiaLite (<c>mod_spatialite</c>) into each Microsoft.Data.Sqlite connection the context opens when the model can
///     map geometry columns, that is, with <c>UseNetTopologySuite()</c>, as Microsoft's provider does. A library that can't be
///     found fails the open. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Infrastructure/SpatialiteLoader.cs
/// </summary>
internal sealed class SqliteSpatialiteInterceptor(IRelationalTypeMappingSource typeMappingSource) : DbConnectionInterceptor
{
    private readonly bool _spatial = typeMappingSource.FindMapping("GEOMETRY") != null;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Load(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Load(connection);
        return Task.CompletedTask;
    }

    private void Load(DbConnection connection)
    {
        if (_spatial && (connection is SqliteConnection sqlite))
        {
            sqlite.LoadExtension("mod_spatialite");
        }
    }
}

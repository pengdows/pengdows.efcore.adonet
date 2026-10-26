namespace pengdows.efcore.adonet;

/// <summary>How a spatial value is held in a column and passed as a parameter.</summary>
public enum SpatialBinary
{
    /// <summary>OGC well-known binary, the SRID kept by the column.</summary>
    Wkb,

    /// <summary>A four-byte little-endian SRID, then well-known binary.</summary>
    SridPrefixedWkb
}

/// <summary>
///     Spatial columns and the functions over them, for the <c>pengdows.efcore.adonet.NetTopologySuite</c> package, which maps
///     NetTopologySuite's types through it; the core itself has no spatial types. Functions are templates keyed by the
///     NetTopologySuite member they translate (<c>Area</c>, <c>Distance</c>, <c>GetGeometryN</c>...): <c>{0}</c> is the
///     geometry, <c>{1}</c>... its arguments. ISO SQL/MM's <c>ST_</c> functions by default.
/// </summary>
public sealed class SpatialSyntax
{
    private static readonly Dictionary<string, string> IsoFunctions = new()
    {
        ["Area"] = "ST_Area({0})",
        ["Boundary"] = "ST_Boundary({0})",
        ["Centroid"] = "ST_Centroid({0})",
        ["Dimension"] = "ST_Dimension({0})",
        ["Envelope"] = "ST_Envelope({0})",
        ["InteriorPoint"] = "ST_PointOnSurface({0})",
        ["PointOnSurface"] = "ST_PointOnSurface({0})",
        ["IsEmpty"] = "ST_IsEmpty({0})",
        ["IsSimple"] = "ST_IsSimple({0})",
        ["IsValid"] = "ST_IsValid({0})",
        ["Length"] = "ST_Length({0})",
        ["NumGeometries"] = "ST_NumGeometries({0})",
        ["NumPoints"] = "ST_NumPoints({0})",
        ["SRID"] = "ST_SRID({0})",
        ["AsBinary"] = "ST_AsBinary({0})",
        ["AsText"] = "ST_AsText({0})",
        ["Buffer"] = "ST_Buffer({0}, {1})",
        ["Contains"] = "ST_Contains({0}, {1})",
        ["ConvexHull"] = "ST_ConvexHull({0})",
        ["Crosses"] = "ST_Crosses({0}, {1})",
        ["Difference"] = "ST_Difference({0}, {1})",
        ["Disjoint"] = "ST_Disjoint({0}, {1})",
        ["Distance"] = "ST_Distance({0}, {1})",
        ["EqualsTopologically"] = "ST_Equals({0}, {1})",
        ["Intersection"] = "ST_Intersection({0}, {1})",
        ["Intersects"] = "ST_Intersects({0}, {1})",
        ["IsWithinDistance"] = "(ST_Distance({0}, {1}) <= {2})",
        ["Overlaps"] = "ST_Overlaps({0}, {1})",
        ["Relate"] = "ST_Relate({0}, {1}, {2})",
        ["SymmetricDifference"] = "ST_SymDifference({0}, {1})",
        ["Touches"] = "ST_Touches({0}, {1})",
        ["Union"] = "ST_Union({0}, {1})",
        ["Within"] = "ST_Within({0}, {1})",
        // .NET's indexes are 0-based, SQL/MM's 1-based.
        ["GetGeometryN"] = "ST_GeometryN({0}, {1} + 1)",
        ["X"] = "ST_X({0})",
        ["Y"] = "ST_Y({0})",
        ["Z"] = "ST_Z({0})",
        ["M"] = "ST_M({0})",
        ["StartPoint"] = "ST_StartPoint({0})",
        ["EndPoint"] = "ST_EndPoint({0})",
        ["IsClosed"] = "ST_IsClosed({0})",
        ["IsRing"] = "ST_IsRing({0})",
        ["GetPointN"] = "ST_PointN({0}, {1} + 1)",
        ["ExteriorRing"] = "ST_ExteriorRing({0})",
        ["NumInteriorRings"] = "ST_NumInteriorRing({0})",
        ["GetInteriorRingN"] = "ST_InteriorRingN({0}, {1} + 1)",
        // SQL/MM names types ST_Point...; NetTopologySuite names them Point...
        ["GeometryType"] = "CASE ST_GeometryType({0}) WHEN 'ST_Point' THEN 'Point' WHEN 'ST_LineString' THEN 'LineString' "
            + "WHEN 'ST_Polygon' THEN 'Polygon' WHEN 'ST_MultiPoint' THEN 'MultiPoint' WHEN 'ST_MultiLineString' THEN 'MultiLineString' "
            + "WHEN 'ST_MultiPolygon' THEN 'MultiPolygon' WHEN 'ST_GeomCollection' THEN 'GeometryCollection' END",
        ["OgcGeometryType"] = "CASE ST_GeometryType({0}) WHEN 'ST_Point' THEN 1 WHEN 'ST_LineString' THEN 2 WHEN 'ST_Polygon' THEN 3 "
            + "WHEN 'ST_MultiPoint' THEN 4 WHEN 'ST_MultiLineString' THEN 5 WHEN 'ST_MultiPolygon' THEN 6 WHEN 'ST_GeomCollection' THEN 7 END"
    };

    public static SpatialSyntax Iso { get; } = new();

    /// <summary>How values are stored and passed.</summary>
    public SpatialBinary Binary { get; init; } = SpatialBinary.Wkb;

    /// <summary>Whether coordinates keep Z and M; where they don't, only X and Y are written.</summary>
    public bool StoresZAndM { get; init; } = true;

    /// <summary>
    ///     A constant from its well-known text <c>{0}</c> (a string literal) and SRID <c>{1}</c>.
    /// </summary>
    public string FromText { get; init; } = "ST_GeomFromText({0}, {1})";

    /// <summary>
    ///     The column type for a geometry of the kind named (<c>Geometry</c>, <c>Point</c>, <c>LineString</c>,
    ///     <c>Polygon</c>, <c>MultiPoint</c>, <c>MultiLineString</c>, <c>MultiPolygon</c>, <c>GeometryCollection</c>).
    /// </summary>
    public Func<string, string> StoreType { get; init; } = kind => kind.ToUpperInvariant();

    /// <summary>Templates that replace or add to ISO's, by member name; a null template leaves the member untranslated.</summary>
    public IReadOnlyDictionary<string, string?> Functions { get; init; } = new Dictionary<string, string?>();

    /// <summary>The template for the member named, or null when it is untranslated.</summary>
    public string? Function(string member)
        => Functions.TryGetValue(member, out var template) ? template : IsoFunctions.GetValueOrDefault(member);
}

// Behaviour reference (not copied): EF Core's SQLite NetTopologySuite mapping plugin (MIT, .NET Foundation):
// https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Storage/Internal/SqliteNetTopologySuiteTypeMappingSourcePlugin.cs

using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace pengdows.efcore.adonet.NetTopologySuite;

/// <summary>
///     Maps NetTopologySuite's geometry types to the dialect's spatial columns, and those columns back to them.
/// </summary>
public class AdoNetSpatialTypeMappingSourcePlugin(NtsGeometryServices geometryServices, ISqlDialect sql) : IRelationalTypeMappingSourcePlugin
{
    // Most specific first: a type is mapped as the first kind it is assignable to.
    private static readonly (string Kind, Type Type)[] Kinds =
    [
        ("Point", typeof(Point)),
        ("LineString", typeof(LineString)),
        ("Polygon", typeof(Polygon)),
        ("MultiPoint", typeof(MultiPoint)),
        ("MultiLineString", typeof(MultiLineString)),
        ("MultiPolygon", typeof(MultiPolygon)),
        ("GeometryCollection", typeof(GeometryCollection)),
        ("Geometry", typeof(Geometry))
    ];

    public virtual RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        if (sql.Spatial is not { } spatial)
        {
            return null;
        }

        var kind = ResolveKind(mappingInfo, spatial);
        if (kind is not { } resolved)
        {
            return null;
        }

        var clrType = mappingInfo.ClrType;
        var storeType = mappingInfo.StoreTypeName;
        return (RelationalTypeMapping)Activator.CreateInstance(
            typeof(AdoNetGeometryTypeMapping<>).MakeGenericType(clrType ?? resolved.Type),
            geometryServices,
            storeType ?? spatial.StoreType(resolved.Kind),
            sql)!;
    }

    private static (string Kind, Type Type)? ResolveKind(RelationalTypeMappingInfo info, SpatialSyntax spatial)
    {
        if (info.ClrType != null && typeof(Geometry).IsAssignableFrom(info.ClrType))
        {
            foreach (var candidate in Kinds)
            {
                if (candidate.Type.IsAssignableFrom(info.ClrType))
                {
                    return candidate;
                }
            }

            return null;
        }

        var storeType = info.StoreTypeNameBase ?? info.StoreTypeName;
        if (storeType == null)
        {
            return null;
        }

        foreach (var candidate in Kinds)
        {
            if (string.Equals(spatial.StoreType(candidate.Kind), storeType, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}

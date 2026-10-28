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
        var mappingType = spatial.ParameterBinding == SpatialParameterBinding.UserDefinedType
            ? typeof(AdoNetSqlServerGeometryTypeMapping<>).MakeGenericType(clrType ?? resolved.Type)
            : typeof(AdoNetGeometryTypeMapping<>).MakeGenericType(clrType ?? resolved.Type);
        var resolvedStoreType = storeType ?? spatial.StoreType(resolved.Kind);
        return (RelationalTypeMapping)(mappingType == typeof(AdoNetSqlServerGeometryTypeMapping<>).MakeGenericType(clrType ?? resolved.Type)
            ? Activator.CreateInstance(mappingType, geometryServices, resolvedStoreType)!
            : Activator.CreateInstance(mappingType, geometryServices, resolvedStoreType, sql)!);
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

        return Kind(spatial, storeType) ?? (spatial.Binary == SpatialBinary.Gaia ? Kind(spatial, WithoutOrdinates(storeType)) : null);
    }

    private static (string Kind, Type Type)? Kind(SpatialSyntax spatial, string storeType)
    {
        foreach (var candidate in Kinds)
        {
            if (string.Equals(spatial.StoreType(candidate.Kind), storeType, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    // A GAIA column's type may name its coordinates: POINTZ, POINTM, POINTZM.
    private static string WithoutOrdinates(string storeType)
        => storeType.EndsWith("ZM", StringComparison.OrdinalIgnoreCase)
            ? storeType[..^2]
            : (storeType.EndsWith('Z') || storeType.EndsWith('z') || storeType.EndsWith('M') || storeType.EndsWith('m'))
                ? storeType[..^1]
                : storeType;
}

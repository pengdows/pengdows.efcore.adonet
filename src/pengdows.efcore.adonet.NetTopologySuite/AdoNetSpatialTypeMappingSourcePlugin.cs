// Adapted from EF Core's SQLite NetTopologySuite plugin (SqliteNetTopologySuiteTypeMappingSourcePlugin).
// Copyright (c) .NET Foundation. Licensed under the MIT license.

using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace pengdows.efcore.adonet.NetTopologySuite;

/// <summary>
///     Maps NetTopologySuite's geometry types to the dialect's spatial columns, and those columns back to them. Adapted from
///     EF Core's SQLite NetTopologySuite plugin (MIT, .NET Foundation):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Storage/Internal/SqliteNetTopologySuiteTypeMappingSourcePlugin.cs
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

        var clrType = mappingInfo.ClrType;
        var storeType = mappingInfo.StoreTypeName;
        var storeTypeBase = mappingInfo.StoreTypeNameBase ?? storeType;
        var kind = clrType != null && typeof(Geometry).IsAssignableFrom(clrType)
            ? Kinds.First(k => k.Type.IsAssignableFrom(clrType))
            : storeType != null
                ? Kinds.FirstOrDefault(k => string.Equals(spatial.StoreType(k.Kind), storeTypeBase, StringComparison.OrdinalIgnoreCase))
                : default;
        if (kind.Type == null)
        {
            return null;
        }

        return (RelationalTypeMapping)Activator.CreateInstance(
            typeof(AdoNetGeometryTypeMapping<>).MakeGenericType(clrType ?? kind.Type),
            geometryServices,
            storeType ?? spatial.StoreType(kind.Kind),
            sql)!;
    }
}

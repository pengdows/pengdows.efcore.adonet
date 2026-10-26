// Portions adapted from EF Core's SQLite NetTopologySuite plugin (SqliteGeometryTypeMapping).
// Copyright (c) .NET Foundation. Licensed under the MIT license.

using System.Buffers.Binary;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace pengdows.efcore.adonet.NetTopologySuite;

/// <summary>
///     A NetTopologySuite geometry stored as the dialect's <see cref="SpatialBinary" />, its constants written through
///     <see cref="SpatialSyntax.FromText" />. Adapted from EF Core's SQLite NetTopologySuite plugin (MIT, .NET Foundation):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Storage/Internal/SqliteGeometryTypeMapping.cs
/// </summary>
public class AdoNetGeometryTypeMapping<TGeometry> : RelationalGeometryTypeMapping<TGeometry, byte[]>
    where TGeometry : Geometry
{
    private static readonly MethodInfo GetBytes
        = typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.GetFieldValue), [typeof(int)])!.MakeGenericMethod(typeof(byte[]));

    private readonly SpatialSyntax _spatial;
    private readonly ISqlDialect _sql;

    public AdoNetGeometryTypeMapping(NtsGeometryServices geometryServices, string storeType, ISqlDialect sql)
        : base(SpatialValueConverter(geometryServices, sql.Spatial!), storeType, JsonGeometryWktReaderWriter.Instance)
    {
        _sql = sql;
        _spatial = sql.Spatial!;
    }

    protected AdoNetGeometryTypeMapping(
        RelationalTypeMappingParameters parameters,
        ValueConverter<TGeometry, byte[]>? converter,
        ISqlDialect sql)
        : base(parameters, converter)
    {
        _sql = sql;
        _spatial = sql.Spatial!;
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetGeometryTypeMapping<TGeometry>(parameters, SpatialConverter, _sql);

    protected override string GenerateNonNullSqlLiteral(object value)
    {
        var geometry = (Geometry)value;
        return _spatial.FromText
            .Replace("{0}", _sql.StringLiteral(geometry.AsText()), StringComparison.Ordinal)
            .Replace("{1}", geometry.SRID.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    public override MethodInfo GetDataReaderMethod()
        => GetBytes;

    protected override string AsText(object value)
        => ((Geometry)value).AsText();

    protected override int GetSrid(object value)
        => ((Geometry)value).SRID;

    protected override Type WktReaderType
        => typeof(WKTReader);

    private static ValueConverter<TGeometry, byte[]> SpatialValueConverter(NtsGeometryServices services, SpatialSyntax spatial)
    {
        var reader = new WKBReader(services);
        return new ValueConverter<TGeometry, byte[]>(
            g => Write(g, spatial.Binary, spatial.StoresZAndM),
            b => (TGeometry)Read(reader, b, spatial.Binary));
    }

    // Z and M are written where the geometry has them and the database keeps them.
    private static byte[] Write(Geometry geometry, SpatialBinary binary, bool zAndM)
    {
        var ordinates = Ordinates.XY;
        if (zAndM && geometry.Coordinates.Any(c => !double.IsNaN(c.Z)))
        {
            ordinates |= Ordinates.Z;
        }

        if (zAndM && geometry.Coordinates.Any(c => !double.IsNaN(c.M)))
        {
            ordinates |= Ordinates.M;
        }

        var wkb = new WKBWriter(ByteOrder.LittleEndian, handleSRID: false, emitZ: ordinates.HasFlag(Ordinates.Z), emitM: ordinates.HasFlag(Ordinates.M))
            .Write(geometry);
        if (binary == SpatialBinary.Wkb)
        {
            return wkb;
        }

        var prefixed = new byte[wkb.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(prefixed, geometry.SRID);
        wkb.CopyTo(prefixed, 4);
        return prefixed;
    }

    private static Geometry Read(WKBReader reader, byte[] bytes, SpatialBinary binary)
    {
        if (binary == SpatialBinary.Wkb)
        {
            return reader.Read(bytes);
        }

        var geometry = reader.Read(bytes.AsSpan(4).ToArray());
        geometry.SRID = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        return geometry;
    }
}

// Behaviour reference (not copied): EF Core's SQLite NetTopologySuite geometry mapping (MIT, .NET Foundation):
// https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Storage/Internal/SqliteGeometryTypeMapping.cs

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
///     <see cref="SpatialSyntax.FromText" />.
/// </summary>
public class AdoNetGeometryTypeMapping<TGeometry> : RelationalGeometryTypeMapping<TGeometry, byte[]>
    where TGeometry : Geometry
{
    private static readonly MethodInfo GetBytes
        = typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.GetFieldValue), [typeof(int)])!.MakeGenericMethod(typeof(byte[]));

    private readonly SpatialSyntax _spatial;
    private readonly ISqlDialect _sql;

    public AdoNetGeometryTypeMapping(NtsGeometryServices geometryServices, string storeType, ISqlDialect sql)
        : base(SpatialValueConverter(geometryServices, sql.Spatial!, storeType), storeType, JsonGeometryWktReaderWriter.Instance)
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

    private static ValueConverter<TGeometry, byte[]> SpatialValueConverter(NtsGeometryServices services, SpatialSyntax spatial, string storeType)
    {
        if (spatial.Binary == SpatialBinary.Gaia)
        {
            return GaiaValueConverter(services, storeType);
        }

        var reader = new WKBReader(services);
        return new ValueConverter<TGeometry, byte[]>(
            g => Encode(g, spatial.Binary, spatial.StoresZAndM),
            b => (TGeometry)Decode(reader, b, spatial.Binary));
    }

    // The GAIA blob, its coordinates those the column's type names: POINT is XY, POINTZ XYZ, POINTM XYM, POINTZM all four.
    // Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Storage/Internal/SqliteGeometryTypeMapping.cs
    private static ValueConverter<TGeometry, byte[]> GaiaValueConverter(NtsGeometryServices services, string storeType)
    {
        var reader = new GaiaGeoReader(services.DefaultCoordinateSequenceFactory, services.DefaultPrecisionModel);
        var writer = new GaiaGeoWriter { HandleOrdinates = StoreTypeOrdinates(storeType) };
        return new ValueConverter<TGeometry, byte[]>(g => writer.Write(g), b => (TGeometry)reader.Read(b));
    }

    internal static Ordinates StoreTypeOrdinates(string storeType)
    {
        var name = storeType.ToUpperInvariant();
        if (name.EndsWith("ZM", StringComparison.Ordinal))
        {
            return Ordinates.XYZM;
        }

        if (name.EndsWith('Z'))
        {
            return Ordinates.XYZ;
        }

        return name.EndsWith('M') ? Ordinates.XYM : Ordinates.XY;
    }

    // Z and M are written where the geometry has them and the database keeps them.
    private static byte[] Encode(Geometry geometry, SpatialBinary binary, bool zAndM)
    {
        var ordinates = GeometryOrdinates(geometry, zAndM);

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

    private static Ordinates GeometryOrdinates(Geometry geometry, bool preserveMeasures)
    {
        var ordinates = Ordinates.XY;
        if (!preserveMeasures)
        {
            return ordinates;
        }

        if (geometry.Coordinates.Any(coordinate => !double.IsNaN(coordinate.Z)))
        {
            ordinates |= Ordinates.Z;
        }

        if (geometry.Coordinates.Any(coordinate => !double.IsNaN(coordinate.M)))
        {
            ordinates |= Ordinates.M;
        }

        return ordinates;
    }

    private static Geometry Decode(WKBReader reader, byte[] bytes, SpatialBinary binary)
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
